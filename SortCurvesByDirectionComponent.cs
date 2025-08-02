using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class SortCurvesByDirectionComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SortCurvesByDirectionComponent class.
        /// </summary>
        public SortCurvesByDirectionComponent()
          : base("Curve Direction Sorter", "DirSort",
              "Sorts curve segments into horizontal, vertical, and diagonal directions relative to a specified plane",
              "Mantis", "Analysis")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Reference plane for direction analysis", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddCurveParameter("Curve", "C", "Input polyline or curve to analyze", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance for direction classification", GH_ParamAccess.item, 0.1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Horizontal", "H", "Horizontal curve segments", GH_ParamAccess.list);
            pManager.AddCurveParameter("Vertical", "V", "Vertical curve segments", GH_ParamAccess.list);
            pManager.AddCurveParameter("Diagonal", "D", "Diagonal curve segments", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input variables
            Plane referencePlane = Plane.WorldXY;
            Curve inputCurve = null;
            double tolerance = 0.1;

            // Get inputs
            if (!DA.GetData(0, ref referencePlane)) return;
            if (!DA.GetData(1, ref inputCurve)) return;
            if (!DA.GetData(2, ref tolerance)) return;

            // Validate inputs
            if (inputCurve == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input curve is null");
                return;
            }

            if (!referencePlane.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Reference plane is invalid");
                return;
            }

            if (tolerance <= 0)
            {
                tolerance = 0.1;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Tolerance must be positive. Using default value 0.1");
            }

            // Initialize output lists
            List<Curve> horizontalCurves = new List<Curve>();
            List<Curve> verticalCurves = new List<Curve>();
            List<Curve> diagonalCurves = new List<Curve>();

            try
            {
                // Explode the curve into individual segments
                Curve[] segments = inputCurve.DuplicateSegments();

                // Process each segment
                foreach (Curve segment in segments)
                {
                    if (segment == null) continue;

                    // Get the start and end points of the segment
                    Point3d startPoint = segment.PointAtStart;
                    Point3d endPoint = segment.PointAtEnd;

                    // Calculate the direction vector of the segment in world coordinates
                    Vector3d worldDirection = endPoint - startPoint;

                    // Transform the direction vector to the reference plane's coordinate system
                    // Project the vector onto the plane's X and Y axes
                    double deltaX = Vector3d.Multiply(worldDirection, referencePlane.XAxis);
                    double deltaY = Vector3d.Multiply(worldDirection, referencePlane.YAxis);

                    // Get absolute values
                    double absDeltaX = Math.Abs(deltaX);
                    double absDeltaY = Math.Abs(deltaY);

                    // Simple classification based on which component dominates
                    if (absDeltaY <= tolerance * absDeltaX)
                    {
                        // Horizontal line (Y change is minimal compared to X relative to plane)
                        horizontalCurves.Add(segment);
                    }
                    else if (absDeltaX <= tolerance * absDeltaY)
                    {
                        // Vertical line (X change is minimal compared to Y relative to plane)
                        verticalCurves.Add(segment);
                    }
                    else
                    {
                        // Diagonal line (both X and Y change significantly relative to plane)
                        diagonalCurves.Add(segment);
                    }
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error processing curve: {ex.Message}");
                return;
            }

            // Set outputs
            DA.SetDataList(0, horizontalCurves);
            DA.SetDataList(1, verticalCurves);
            DA.SetDataList(2, diagonalCurves);
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
            get { return new Guid("2056D179-50CD-45E5-8C52-BB4B3A2CE5AA"); }
        }
    }
}