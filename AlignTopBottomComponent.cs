using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class AlignTopBottomComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AlignTopBottomComponent class.
        /// </summary>
        public AlignTopBottomComponent()
          : base("Align Top Bottom", "Mantis",
              "Aligns the seams and directions of two closed curves for better lofting",
              "Mantis", "Curves")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Top", "T", "Top curve to align", GH_ParamAccess.item);
            pManager.AddCurveParameter("Bottom", "B", "Bottom curve to align", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Top", "T", "Aligned top curve", GH_ParamAccess.item);
            pManager.AddCurveParameter("Bottom", "B", "Aligned bottom curve", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve top = null;
            Curve bottom = null;

            if (!DA.GetData(0, ref top)) return;
            if (!DA.GetData(1, ref bottom)) return;

            // Handle null inputs
            if (bottom == null || top == null)
            {
                DA.SetData(0, top);
                DA.SetData(1, bottom);
                return;
            }

            // Work with copies
            Curve alignedBottom = bottom.DuplicateCurve();
            Curve alignedTop = top.DuplicateCurve();

            // Step 1: Find the best seam alignment
            AlignCurveSeams(ref alignedBottom, ref alignedTop);

            // Step 2: Ensure both curves have the same direction
            AlignCurveDirections(ref alignedBottom, ref alignedTop);

            DA.SetData(0, alignedTop);
            DA.SetData(1, alignedBottom);
        }

        private void AlignCurveSeams(ref Curve bottom, ref Curve top)
        {
            if (!bottom.IsClosed || !top.IsClosed) return;

            // Get multiple reference points from bottom curve for better alignment
            List<Point3d> bottomRefPoints = new List<Point3d>();
            int refPointCount = 8; // Use more reference points for better accuracy

            for (int i = 0; i < refPointCount; i++)
            {
                double t = bottom.Domain.Min + (bottom.Domain.Max - bottom.Domain.Min) * i / refPointCount;
                bottomRefPoints.Add(bottom.PointAt(t));
            }

            double bestSeamParam = top.Domain.Min;
            double minTotalDistance = double.MaxValue;
            bool shouldReverse = false;

            // Test different seam positions and orientations
            int samples = 100;
            for (int i = 0; i < samples; i++)
            {
                double testSeam = top.Domain.Min + (top.Domain.Max - top.Domain.Min) * i / samples;

                // Test normal orientation
                Curve tempTop = top.DuplicateCurve();
                tempTop.ChangeClosedCurveSeam(testSeam);
                double normalDistance = CalculateAlignmentDistance(bottomRefPoints, tempTop);

                // Test reversed orientation
                tempTop.Reverse();
                double reversedDistance = CalculateAlignmentDistance(bottomRefPoints, tempTop);

                // Choose the better orientation
                if (normalDistance < minTotalDistance)
                {
                    minTotalDistance = normalDistance;
                    bestSeamParam = testSeam;
                    shouldReverse = false;
                }

                if (reversedDistance < minTotalDistance)
                {
                    minTotalDistance = reversedDistance;
                    bestSeamParam = testSeam;
                    shouldReverse = true;
                }
            }

            // Apply the best seam position
            if (Math.Abs(bestSeamParam - top.Domain.Min) > 1e-6)
            {
                top.ChangeClosedCurveSeam(bestSeamParam);
            }

            // Apply reversal if needed
            if (shouldReverse)
            {
                top.Reverse();
            }
        }

        private double CalculateAlignmentDistance(List<Point3d> bottomRefPoints, Curve topCurve)
        {
            double totalDistance = 0.0;
            int pointCount = bottomRefPoints.Count;

            for (int i = 0; i < pointCount; i++)
            {
                // Calculate corresponding parameter on top curve
                double t = topCurve.Domain.Min + (topCurve.Domain.Max - topCurve.Domain.Min) * i / pointCount;
                Point3d topPoint = topCurve.PointAt(t);

                // Calculate distance to corresponding bottom point
                totalDistance += bottomRefPoints[i].DistanceTo(topPoint);
            }

            return totalDistance;
        }

        private void AlignCurveDirections(ref Curve bottom, ref Curve top)
        {
            // This method is now integrated into AlignCurveSeams for better performance
            // but we'll keep it for additional validation if needed

            if (!bottom.IsClosed || !top.IsClosed) return;

            // Sample tangent vectors at multiple points to verify alignment
            int sampleCount = 12;
            double totalDotProduct = 0.0;

            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / (sampleCount - 1);

                double bottomParam = bottom.Domain.Min + t * (bottom.Domain.Max - bottom.Domain.Min);
                double topParam = top.Domain.Min + t * (top.Domain.Max - top.Domain.Min);

                Vector3d bottomTangent = bottom.TangentAt(bottomParam);
                Vector3d topTangent = top.TangentAt(topParam);

                // Normalize tangents
                bottomTangent.Unitize();
                topTangent.Unitize();

                // Calculate dot product for direction comparison
                double dotProduct = bottomTangent * topTangent;
                totalDotProduct += dotProduct;
            }

            // If the average dot product suggests opposite directions, reverse
            // This serves as a secondary check since seam alignment now handles orientation
            double averageDotProduct = totalDotProduct / sampleCount;
            if (averageDotProduct < -0.3) // Allow some tolerance
            {
                top.Reverse();
            }
        }

        // Alternative method using closest point analysis (kept for reference/future use)
        private void AlignCurveSeamsAdvanced(ref Curve bottom, ref Curve top)
        {
            if (!bottom.IsClosed || !top.IsClosed) return;

            // Get multiple reference points from bottom curve
            List<Point3d> bottomPoints = new List<Point3d>();
            List<double> bottomParams = new List<double>();

            int refPoints = 4; // Use 4 reference points for better alignment
            for (int i = 0; i < refPoints; i++)
            {
                double t = bottom.Domain.Min + (bottom.Domain.Max - bottom.Domain.Min) * i / refPoints;
                bottomPoints.Add(bottom.PointAt(t));
                bottomParams.Add(t);
            }

            double bestSeamParam = top.Domain.Min;
            double minTotalDistance = double.MaxValue;

            // Test different seam positions
            int testCount = 50;
            for (int i = 0; i < testCount; i++)
            {
                double testSeam = top.Domain.Min + (top.Domain.Max - top.Domain.Min) * i / testCount;

                // Create temporary curve with this seam
                Curve tempTop = top.DuplicateCurve();
                tempTop.ChangeClosedCurveSeam(testSeam);

                // Calculate total distance for all reference points
                double totalDistance = 0.0;
                for (int j = 0; j < refPoints; j++)
                {
                    double correspondingParam = tempTop.Domain.Min + (tempTop.Domain.Max - tempTop.Domain.Min) * j / refPoints;
                    Point3d topPoint = tempTop.PointAt(correspondingParam);
                    totalDistance += bottomPoints[j].DistanceTo(topPoint);
                }

                if (totalDistance < minTotalDistance)
                {
                    minTotalDistance = totalDistance;
                    bestSeamParam = testSeam;
                }
            }

            // Apply best seam
            if (Math.Abs(bestSeamParam - top.Domain.Min) > 1e-6)
            {
                top.ChangeClosedCurveSeam(bestSeamParam);
            }
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
            get { return new Guid("DB11ADF2-7EB3-4698-A5FD-138004C0E690"); }
        }
    }
}