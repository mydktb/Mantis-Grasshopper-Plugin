using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Mantis
{
    public class OffsetGeometryComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the OffsetGeometryComponent class.
        /// </summary>
        public OffsetGeometryComponent()
           : base("Offset Geometry", "OffsetGeo",
              "Offset geometry along their computed normals",
              "Mantis", "Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Geometry to offset", GH_ParamAccess.list);
            pManager.AddNumberParameter("Offset", "O", "Offset distance", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Flip Direction", "F", "Flip the normal direction", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Offset Geometry", "OG", "Offsetted geometry", GH_ParamAccess.list);
            pManager.AddVectorParameter("Normals", "N", "Computed normal vectors", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> geometry = new List<GeometryBase>();
            double offset = 1.0;
            bool flipDirection = false;

            if (!DA.GetDataList(0, geometry)) return;
            if (!DA.GetData(1, ref offset)) return;
            if (!DA.GetData(2, ref flipDirection)) return;

            List<Vector3d> normals = new List<Vector3d>();
            List<GeometryBase> offsetedGeo = new List<GeometryBase>();

            foreach (GeometryBase geo in geometry)
            {
                // Create a copy of the geometry to avoid modifying the original
                GeometryBase geoCopy = geo.Duplicate();

                Vector3d normal = Vector3d.Zero;
                Point3d basePoint = Point3d.Origin;

                // Handle different geometry types
                if (geoCopy is Surface)
                {
                    Surface srf = geoCopy as Surface;
                    double u = srf.Domain(0).Mid;
                    double v = srf.Domain(1).Mid;
                    normal = srf.NormalAt(u, v);
                    basePoint = srf.PointAt(u, v);
                }
                else if (geoCopy is Brep)
                {
                    Brep brep = geoCopy as Brep;
                    if (brep.Faces.Count > 0)
                    {
                        BrepFace face = brep.Faces[0];
                        double u = face.Domain(0).Mid;
                        double v = face.Domain(1).Mid;
                        normal = face.NormalAt(u, v);
                        basePoint = face.PointAt(u, v);
                    }
                    else
                    {
                        basePoint = brep.GetBoundingBox(true).Center;
                    }
                }
                else if (geoCopy is Curve)
                {
                    Curve crv = geoCopy as Curve;
                    double t = crv.Domain.Mid;
                    basePoint = crv.PointAt(t);

                    if (crv.IsClosed)
                    {
                        Brep[] breps = Brep.CreatePlanarBreps(crv, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (breps != null && breps.Length > 0)
                        {
                            BrepFace face = breps[0].Faces[0];
                            double u = face.Domain(0).Mid;
                            double v = face.Domain(1).Mid;
                            normal = face.NormalAt(u, v);
                            basePoint = face.PointAt(u, v);
                        }
                        else
                        {
                            normal = new Vector3d(0, 0, 1);
                        }
                    }
                    else
                    {
                        Vector3d tangent = crv.TangentAt(t);
                        Vector3d worldZ = new Vector3d(0, 0, 1);
                        normal = Vector3d.CrossProduct(tangent, worldZ);

                        if (normal.Length < 0.001)
                        {
                            Vector3d worldX = new Vector3d(1, 0, 0);
                            normal = Vector3d.CrossProduct(tangent, worldX);
                        }
                    }
                }
                else if (geoCopy is Mesh)
                {
                    Mesh mesh = geoCopy as Mesh;
                    basePoint = mesh.GetBoundingBox(true).Center;

                    if (mesh.Faces.Count > 0)
                    {
                        if (mesh.FaceNormals.Count == 0)
                        {
                            mesh.FaceNormals.ComputeFaceNormals();
                        }

                        if (mesh.FaceNormals.Count > 0)
                        {
                            normal = mesh.FaceNormals[0];
                        }
                        else
                        {
                            normal = new Vector3d(0, 0, 1);
                        }
                    }
                    else
                    {
                        normal = new Vector3d(0, 0, 1);
                    }
                }

                // Ensure normal is valid and process it
                if (normal.Length > 0.001)
                {
                    normal.Unitize();

                    // Make sure normal points upward (positive Z component)
                    if (normal.Z < 0) normal.Reverse();
                    if (flipDirection) normal.Reverse();

                    // Scale by offset
                    normal *= offset;
                }

                // Apply transformation
                normals.Add(normal);
                Transform offsetTrans = Transform.Translation(normal);
                geoCopy.Transform(offsetTrans);
                offsetedGeo.Add(geoCopy);
            }

            DA.SetDataList(0, offsetedGeo);
            DA.SetDataList(1, normals);
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
            get { return new Guid("13C6C3E5-0377-4700-A5FE-E8F204C1925C"); }
        }
    }
}