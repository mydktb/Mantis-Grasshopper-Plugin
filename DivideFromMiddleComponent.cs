using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class DivideFromMiddleComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DivideFromMiddleComponent class.
        /// </summary>
        public DivideFromMiddleComponent()
          : base("Divide From Middle", "DivMid",
              "Divides a curve into segments of specified length starting from the midpoint and working outward",
              "Mantis", "Curves")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Curve to divide", GH_ParamAccess.item);
            pManager.AddNumberParameter("Segment Length", "L", "Length of each segment", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Division Points", "P", "Points where curve is divided", GH_ParamAccess.list);
            pManager.AddCurveParameter("Segments", "S", "Middle segments of specified length", GH_ParamAccess.list);
            pManager.AddCurveParameter("Start Segment", "SS", "Remaining segment at start of curve", GH_ParamAccess.item);
            pManager.AddCurveParameter("End Segment", "ES", "Remaining segment at end of curve", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve curve = null;
            double segmentLength = 0.0;

            if (!DA.GetData(0, ref curve)) return;
            if (!DA.GetData(1, ref segmentLength)) return;

            if (curve == null || segmentLength <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid input: Curve is null or segment length is not positive");
                return;
            }

            double totalLength = curve.GetLength();
            var points = new List<Point3d>();
            var parameters = new List<double>();

            // Get midpoint parameter
            double midParam;
            if (curve.LengthParameter(totalLength / 2.0, out midParam))
            {
                parameters.Add(midParam);
                points.Add(curve.PointAt(midParam));
            }

            // Step forward from midpoint
            double forwardLength = totalLength / 2.0;
            for (int i = 1; forwardLength + i * segmentLength <= totalLength; i++)
            {
                double t;
                if (curve.LengthParameter(forwardLength + i * segmentLength, out t))
                {
                    parameters.Add(t);
                    points.Add(curve.PointAt(t));
                }
            }

            // Step backward from midpoint
            for (int i = 1; forwardLength - i * segmentLength >= 0; i++)
            {
                double t;
                if (curve.LengthParameter(forwardLength - i * segmentLength, out t))
                {
                    parameters.Add(t);
                    points.Add(curve.PointAt(t));
                }
            }

            // Sort parameters along the curve
            parameters.Sort();

            // Create segments between consecutive parameters
            var curveSegments = new List<Curve>();
            for (int i = 0; i < parameters.Count - 1; i++)
            {
                Curve segment = curve.Trim(parameters[i], parameters[i + 1]);
                if (segment != null)
                    curveSegments.Add(segment);
            }

            // Create start and end segments
            Curve startSeg = null;
            Curve endSeg = null;

            if (parameters.Count > 0)
            {
                if (parameters[0] > curve.Domain.T0)
                    startSeg = curve.Trim(curve.Domain.T0, parameters[0]);

                if (parameters[parameters.Count - 1] < curve.Domain.T1)
                    endSeg = curve.Trim(parameters[parameters.Count - 1], curve.Domain.T1);
            }

            DA.SetDataList(0, points);
            DA.SetDataList(1, curveSegments);
            DA.SetData(2, startSeg);
            DA.SetData(3, endSeg);
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
            get { return new Guid("ED31CE91-8B12-4608-8369-E8EB6828F669"); }
        }
    }
}