using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class VariablePolylineOffsetComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the VariablePolylineOffsetComponent class.
        /// </summary>
        public VariablePolylineOffsetComponent()
          : base("Variable Polyline Offset", "VarOffset",
              "Offsets a polyline with variable distances for each segment",
              "Mantis", "Curves")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "Pl", "Reference plane for offset direction", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddCurveParameter("Polyline", "P", "Input polyline to offset", GH_ParamAccess.item);
            pManager.AddNumberParameter("Offsets", "O", "List of offset distances for each segment", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Invert", "I", "Invert offset direction", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Tolerance", "T", "Intersection tolerance", GH_ParamAccess.item, 0.001);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Polyline", "P", "Offset polyline", GH_ParamAccess.item);
            pManager.AddPointParameter("Corners", "C", "Corner points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input variables
            Plane plane = Plane.WorldXY;
            Curve inputCurve = null;
            List<double> offsets = new List<double>();
            bool invert = false;
            double tolerance = 0.001;

            // Get inputs
            if (!DA.GetData(0, ref plane)) return;
            if (!DA.GetData(1, ref inputCurve)) return;
            if (!DA.GetDataList(2, offsets)) return;
            if (!DA.GetData(3, ref invert)) return;
            if (!DA.GetData(4, ref tolerance)) return;

            // Validate inputs
            if (!plane.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Reference plane is invalid");
                return;
            }

            if (inputCurve == null || !inputCurve.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input curve is null or invalid");
                return;
            }

            if (offsets == null || offsets.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Offset list is empty");
                return;
            }

            // Convert curve to polyline
            Polyline polyline;
            if (!inputCurve.TryGetPolyline(out polyline))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input must be a polyline");
                return;
            }

            if (polyline.SegmentCount < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Polyline must have at least 2 segments");
                return;
            }

            // Process offsets
            List<double> workingOffsets = new List<double>(offsets);
            if (invert)
            {
                workingOffsets = workingOffsets.Select(x => -x).ToList();
            }

            // Calculate offset lines (internal use only)
            List<Line> offsetLines = CalculateOffsetLines(polyline, plane, workingOffsets);

            // Calculate corner points
            List<Point3d> cornerPoints = CalculateCornerPoints(offsetLines, tolerance);

            // Create output polyline (closed if input was closed)
            Polyline resultPolyline = new Polyline(cornerPoints);
            if (polyline.IsClosed && cornerPoints.Count > 1)
            {
                resultPolyline.Add(cornerPoints[0]);
            }

            // Set outputs
            if (resultPolyline.IsValid && resultPolyline.Count >= 2)
            {
                DA.SetData(0, resultPolyline.ToPolylineCurve());
                DA.SetDataList(1, cornerPoints);
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not create valid offset polyline");
            }
        }

        private List<Line> CalculateOffsetLines(Polyline polyline, Plane plane, List<double> offsets)
        {
            List<Line> offsetLines = new List<Line>();

            for (int i = 0; i < polyline.SegmentCount; i++)
            {
                Line segment = polyline.SegmentAt(i);
                Vector3d segmentVec = segment.Direction;
                segmentVec.Unitize();

                Vector3d offsetDir = Vector3d.CrossProduct(segmentVec, plane.ZAxis);
                offsetDir.Unitize();

                double offsetDist = offsets[i % offsets.Count];
                Vector3d offsetVec = offsetDir * offsetDist;

                offsetLines.Add(new Line(segment.From + offsetVec, segment.To + offsetVec));
            }

            return offsetLines;
        }

        private List<Point3d> CalculateCornerPoints(List<Line> offsetLines, double tolerance)
        {
            List<Point3d> corners = new List<Point3d>();

            for (int i = 0; i < offsetLines.Count; i++)
            {
                int nextIdx = (i + 1) % offsetLines.Count;
                Line current = offsetLines[i];
                Line next = offsetLines[nextIdx];

                double a, b;
                if (Rhino.Geometry.Intersect.Intersection.LineLine(current, next, out a, out b, tolerance, true))
                {
                    corners.Add(current.PointAt(a));
                }
                else
                {
                    // Fallback to average point
                    corners.Add((current.To + next.From) * 0.5);
                }
            }

            return corners;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //// You can add an image file to your project resources and access it like this:
                //// return Resources.IconForThisComponent;
                //var iBytes = Properties.Resources.VariablePolylineOffsetIcon;
                //using (MemoryStream memS = new MemoryStream(iBytes))
                //{
                //    System.Drawing.Bitmap image = new System.Drawing.Bitmap(memS);
                //    return image;
                //}

                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3C160CDC-9CCF-4DF8-8693-612173EAC332"); }
        }
    }
}