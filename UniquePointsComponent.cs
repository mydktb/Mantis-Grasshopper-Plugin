using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class UniquePointsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the UniquePointsComponent class.
        /// </summary>
        public UniquePointsComponent()
          : base("Unique Points", "UPts",
              "Remove duplicate points from a list based on distance tolerance",
              "Mantis", "Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "List of points to filter for uniqueness", GH_ParamAccess.list);
            pManager.AddNumberParameter("Tolerance", "T", "Distance tolerance for point comparison", GH_ParamAccess.item, 0.01);

            // Make tolerance optional
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Unique Points", "UP", "List of unique points", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Duplicate Count", "DC", "Number of duplicates removed", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Original Count", "OC", "Original number of points", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> points = new List<Point3d>();
            double tolerance = 0.01;

            // Get input data
            if (!DA.GetDataList(0, points)) return;
            DA.GetData(1, ref tolerance);

            // Input validation
            if (points == null || points.Count == 0)
            {
                DA.SetDataList(0, new List<Point3d>());
                DA.SetData(1, 0);
                DA.SetData(2, 0);
                return;
            }

            // Set default tolerance if not provided or invalid
            if (tolerance <= 0)
                tolerance = Rhino.RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.01;

            int originalCount = points.Count;
            List<Point3d> uniquePoints = new List<Point3d>();

            for (int i = 0; i < points.Count; i++)
            {
                bool isDuplicate = false;

                // Check against all previously added unique points
                for (int j = 0; j < uniquePoints.Count; j++)
                {
                    if (ArePointsEqual(points[i], uniquePoints[j], tolerance))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                // If not a duplicate, add to unique list
                if (!isDuplicate)
                {
                    uniquePoints.Add(points[i]);
                }
            }

            int duplicateCount = originalCount - uniquePoints.Count;

            // Set output data
            DA.SetDataList(0, uniquePoints);
            DA.SetData(1, duplicateCount);
            DA.SetData(2, originalCount);
        }

        /// <summary>
        /// Helper method to check if two points are equal within tolerance
        /// </summary>
        private bool ArePointsEqual(Point3d point1, Point3d point2, double tolerance)
        {
            return point1.DistanceTo(point2) <= tolerance;
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
            get { return new Guid("03B6AA2D-ABCA-429F-8614-E249C1D8BFCE"); }
        }
    }
}