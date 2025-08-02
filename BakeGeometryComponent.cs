using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;


namespace Mantis
{
    public class BakeGeometryComponent : GH_Component
    {

        private static Random _colorRandom = new Random();


        /// <summary>
        /// Initializes a new instance of the BakeGeometryComponent class.
        /// </summary>
        public BakeGeometryComponent()
          : base("Bake Geometry", "Bake",
              "Bakes geometry to Rhino document with custom layer and attributes",
              "Mantis", "Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Bake", "B", "Trigger to bake geometry", GH_ParamAccess.item, false);
            pManager.AddGeometryParameter("Geometry", "G", "Geometry to bake", GH_ParamAccess.list);
            pManager.AddTextParameter("Layer", "L", "Layer name for baked geometry", GH_ParamAccess.item, "Default");
            pManager.AddTextParameter("Object Name", "N", "Name for baked objects", GH_ParamAccess.item, "MantisObject");

            // Make object name optional
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "S", "Baking status message", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Object Count", "C", "Number of objects baked", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Declare variables to hold input data
            bool bake = false;
            List<GeometryBase> geometries = new List<GeometryBase>();
            string layerName = string.Empty;
            string objectName = string.Empty;

            // Retrieve input data
            if (!DA.GetData(0, ref bake)) return;
            if (!DA.GetDataList(1, geometries)) return;
            if (!DA.GetData(2, ref layerName)) return;
            DA.GetData(3, ref objectName);

            string status = "Ready to bake";
            int objectCount = 0;

            if (bake && geometries.Count > 0)
            {
                try
                {
                    RhinoDoc doc = RhinoDoc.ActiveDoc;
                    if (doc == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document found");
                        status = "No active Rhino document";
                        DA.SetData(0, status);
                        DA.SetData(1, objectCount);
                        return;
                    }

                    // Create or find layer
                    int layerIndex = CreateOrFindLayer(doc, layerName);

                    // Set up object attributes
                    ObjectAttributes attributes = new ObjectAttributes();
                    attributes.Name = objectName;
                    attributes.LayerIndex = layerIndex;

                    // Bake each geometry
                    foreach (var geometry in geometries)
                    {
                        if (geometry != null)
                        {
                            Guid objId = BakeGeometry(doc, geometry, attributes);
                            if (objId != Guid.Empty)
                            {
                                objectCount++;
                            }
                        }
                    }

                    doc.Views.Redraw();
                    status = $"Successfully baked {objectCount} objects to layer '{layerName}'";
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Baking failed: {ex.Message}");
                    status = "Baking failed";
                }
            }

            // Assign output data
            DA.SetData(0, status);
            DA.SetData(1, objectCount);
        }

        /// <summary>
        /// Creates a new layer or finds existing layer
        /// </summary>
        private int CreateOrFindLayer(RhinoDoc doc, string layerName)
        {
            var layer = doc.Layers.FindName(layerName);
            if (layer == null)
            {
                // Generate random color
                Color randomColor = Color.FromArgb(
                    _colorRandom.Next(0, 256),
                    _colorRandom.Next(0, 256),
                    _colorRandom.Next(0, 256)
                );

                int newLayerIndex = doc.Layers.Add(layerName, randomColor);
                return newLayerIndex;
            }
            return layer.Index;
        }

        /// <summary>
        /// Bakes geometry based on its type
        /// </summary>
        private Guid BakeGeometry(RhinoDoc doc, GeometryBase geometry, ObjectAttributes attributes)
        {
            Guid objId = Guid.Empty;

            try
            {
                if (geometry is Brep brep)
                {
                    objId = doc.Objects.AddBrep(brep, attributes);
                }
                else if (geometry is Mesh mesh)
                {
                    objId = doc.Objects.AddMesh(mesh, attributes);
                }
                else if (geometry is Curve curve)
                {
                    objId = doc.Objects.AddCurve(curve, attributes);
                }
                else if (geometry is Surface surface)
                {
                    objId = doc.Objects.AddSurface(surface, attributes);
                }
                else if (geometry is Rhino.Geometry.Point rhinoPoint)
                {
                    objId = doc.Objects.AddPoint(rhinoPoint.Location, attributes);
                }

                else if (geometry is TextEntity text)
                {
                    objId = doc.Objects.AddText(text, attributes);
                }
                else
                {
                    // Try generic geometry addition
                    objId = doc.Objects.Add(geometry, attributes);
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Failed to bake geometry type {geometry.GetType().Name}: {ex.Message}");
            }

            return objId;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3FC81369-C7DA-4DEB-BED5-64E22D4D885F"); }
        }
    }
}