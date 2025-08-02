using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Mantis
{
    public class UnrollGeometryComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the UnrollGeometryComponent class.
        /// </summary>
        public UnrollGeometryComponent()
          : base("Unroll Geometry", "UnrollGeo",
              "Unroll panels grouped by coplanar surfaces with organized layout and labeling",
              "Mantis", "Fabrication")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Geometry", "G", "Brep geometry to unroll", GH_ParamAccess.list);
            pManager.AddNumberParameter("Column Spacing", "CS", "Spacing between columns in layout", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("Row Spacing", "RS", "Spacing between rows in layout", GH_ParamAccess.item, 100.0);
            pManager.AddPointParameter("Reference Point", "R", "Reference point for sorting panels", GH_ParamAccess.item, Point3d.Origin);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Flat Panels", "FP", "Unrolled flat panels", GH_ParamAccess.list);
            pManager.AddGenericParameter("Original Labels", "OL", "Text dots for original panel positions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Unrolled Labels", "UL", "Text dots for unrolled panel positions", GH_ParamAccess.list);
            pManager.AddTextParameter("Panel Info", "PI", "Information about panel grouping", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> panels = new List<Brep>();
            double colSpacing = 100.0;
            double rowSpacing = 100.0;
            Point3d referencePoint = Point3d.Origin;

            if (!DA.GetDataList(0, panels)) return;
            if (!DA.GetData(1, ref colSpacing)) return;
            if (!DA.GetData(2, ref rowSpacing)) return;
            if (!DA.GetData(3, ref referencePoint)) return;

            List<Brep> unrolled = new List<Brep>();
            List<TextDot> origDots = new List<TextDot>();
            List<TextDot> flatDots = new List<TextDot>();
            List<string> panelInfo = new List<string>();

            double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            List<Plane> planes = new List<Plane>();
            List<List<Brep>> groupedPanels = new List<List<Brep>>();

            // Group panels by coplanar planes
            for (int i = 0; i < panels.Count; i++)
            {
                Brep brep = panels[i];
                if (brep == null || !brep.IsValid) continue;

                Plane candidatePlane = Plane.Unset;
                bool found = false;

                foreach (BrepFace face in brep.Faces)
                {
                    if (face.IsPlanar(tolerance) && face.TryGetPlane(out candidatePlane))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Panel {i} has no planar faces and will be skipped.");
                    continue;
                }

                int groupIndex = -1;
                for (int j = 0; j < planes.Count; j++)
                {
                    if (AreCoplanar(candidatePlane, planes[j], tolerance))
                    {
                        groupIndex = j;
                        break;
                    }
                }

                if (groupIndex == -1)
                {
                    planes.Add(candidatePlane);
                    groupedPanels.Add(new List<Brep> { brep });
                }
                else
                {
                    groupedPanels[groupIndex].Add(brep);
                }
            }

            // Sort each group by distance to reference point and process
            for (int row = 0; row < groupedPanels.Count; row++)
            {
                List<Brep> group = groupedPanels[row];

                group.Sort((a, b) =>
                {
                    double da = a.GetBoundingBox(true).Center.DistanceTo(referencePoint);
                    double db = b.GetBoundingBox(true).Center.DistanceTo(referencePoint);
                    return da.CompareTo(db);
                });

                string groupLabel = $"Plane {(char)('A' + row)}";
                panelInfo.Add($"{groupLabel}: {group.Count} panels");

                for (int col = 0; col < group.Count; col++)
                {
                    Brep brep = group[col];
                    string panelName = $"{groupLabel}-{col}";

                    // Label original
                    Point3d origCenter = brep.GetBoundingBox(true).Center;
                    origDots.Add(new TextDot(panelName, origCenter));

                    // Unroll
                    try
                    {
                        Unroller unroller = new Unroller(brep);
                        Curve[] curves;
                        Point3d[] points;
                        TextDot[] dots;
                        Brep[] result = unroller.PerformUnroll(out curves, out points, out dots);

                        if (result != null && result.Length > 0)
                        {
                            double x = col * colSpacing;
                            double y = -row * rowSpacing;
                            Transform move = Transform.Translation(x, y, 0);

                            foreach (Brep b in result)
                            {
                                Brep moved = b.DuplicateBrep();
                                moved.Transform(move);
                                unrolled.Add(moved);

                                Point3d center = moved.GetBoundingBox(true).Center;
                                center.Transform(Transform.Translation(10, 10, 0));
                                flatDots.Add(new TextDot(panelName, center));
                            }
                        }
                        else
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to unroll panel {panelName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error unrolling panel {panelName}: {ex.Message}");
                    }
                }
            }

            // Set outputs
            DA.SetDataList(0, unrolled);
            DA.SetDataList(1, origDots);
            DA.SetDataList(2, flatDots);
            DA.SetDataList(3, panelInfo);
        }

        // Helper: Check if two planes are coplanar
        private bool AreCoplanar(Plane a, Plane b, double tol)
        {
            if (a.Normal.IsParallelTo(b.Normal, tol) == 0)
                return false;

            double dist = Math.Abs(Vector3d.Multiply(b.Origin - a.Origin, a.Normal));
            return dist < tol;
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
            get { return new Guid("0276D01C-1DCF-44BE-ABCB-E3D93127DC11"); }
        }
    }
}