using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class UniqueLinesComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the UniqueLinesComponent class.
        /// </summary>
        public UniqueLinesComponent()
          : base("Unique Lines", "ULines",
              "Remove duplicate curves from a list based on geometric similarity",
              "Mantis", "Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "List of curves to filter for uniqueness", GH_ParamAccess.list);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance for curve comparison", GH_ParamAccess.item, 0.01);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Unique Curves", "UC", "List of unique curves", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Duplicate Count", "DC", "Number of duplicates removed", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Original Count", "OC", "Original number of curves", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> curves = new List<Curve>();
            double tolerance = 0.01;

            // Get input data
            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref tolerance);

            // Input validation
            if (curves == null || curves.Count == 0)
            {
                DA.SetDataList(0, new List<Curve>());
                DA.SetData(1, 0);
                DA.SetData(2, 0);
                return;
            }

            // Set default tolerance if not provided or invalid
            if (tolerance <= 0)
                tolerance = Rhino.RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.01;

            int originalCount = curves.Count;
            List<Curve> uniqueCurves = new List<Curve>();

            for (int i = 0; i < curves.Count; i++)
            {
                if (curves[i] == null) continue;

                bool isDuplicate = false;

                // Check against all previously added unique curves
                for (int j = 0; j < uniqueCurves.Count; j++)
                {
                    if (AreCurvesEqual(curves[i], uniqueCurves[j], tolerance))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                // If not a duplicate, add to unique list
                if (!isDuplicate)
                {
                    uniqueCurves.Add(curves[i]);
                }
            }

            int duplicateCount = originalCount - uniqueCurves.Count;

            // Set output data
            DA.SetDataList(0, uniqueCurves);
            DA.SetData(1, duplicateCount);
            DA.SetData(2, originalCount);
        }

        /// <summary>
        /// Helper method to check if two curves are equal
        /// </summary>
        private bool AreCurvesEqual(Curve curve1, Curve curve2, double tolerance)
        {
            // Quick check: if both are lines, compare endpoints
            if (curve1.IsLinear() && curve2.IsLinear())
            {
                Point3d start1 = curve1.PointAtStart;
                Point3d end1 = curve1.PointAtEnd;
                Point3d start2 = curve2.PointAtStart;
                Point3d end2 = curve2.PointAtEnd;

                // Check if curves are identical (same direction)
                bool sameDirection = start1.DistanceTo(start2) <= tolerance &&
                                   end1.DistanceTo(end2) <= tolerance;

                // Check if curves are identical (opposite direction)
                bool oppositeDirection = start1.DistanceTo(end2) <= tolerance &&
                                       end1.DistanceTo(start2) <= tolerance;

                return sameDirection || oppositeDirection;
            }

            // For non-linear curves, use more comprehensive comparison
            // Check if curves have similar length
            if (Math.Abs(curve1.GetLength() - curve2.GetLength()) > tolerance)
                return false;

            // Sample points along both curves and compare
            int sampleCount = 10;
            bool forwardMatch = true;
            bool reverseMatch = true;

            for (int i = 0; i <= sampleCount; i++)
            {
                double t = (double)i / sampleCount;
                Point3d pt1 = curve1.PointAtNormalizedLength(t);
                Point3d pt2 = curve2.PointAtNormalizedLength(t);
                Point3d pt2_rev = curve2.PointAtNormalizedLength(1.0 - t);

                if (pt1.DistanceTo(pt2) > tolerance)
                    forwardMatch = false;

                if (pt1.DistanceTo(pt2_rev) > tolerance)
                    reverseMatch = false;

                if (!forwardMatch && !reverseMatch)
                    return false;
            }

            return forwardMatch || reverseMatch;
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
            get { return new Guid("3CA72661-4F20-46E8-88DD-1D983BED8837"); }
        }
    }
}