using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class CurveLengthFilterComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CurveLengthFilterComponent class.
        /// </summary>
        public CurveLengthFilterComponent()
          : base("Curve Length Filter", "CrvFilter",
              "Separates curves into long and short categories based on a threshold length",
              "Mantis", "Curves")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "List of curves to filter", GH_ParamAccess.list);
            pManager.AddNumberParameter("Threshold", "T", "Length threshold for filtering", GH_ParamAccess.item, 10.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Long Curves", "L", "Curves longer than or equal to threshold", GH_ParamAccess.list);
            pManager.AddCurveParameter("Short Curves", "S", "Curves shorter than threshold", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var curves = new List<Curve>();
            double threshold = 0.0;

            if (!DA.GetDataList(0, curves)) return;
            if (!DA.GetData(1, ref threshold)) return;

            var longer = new List<Curve>();
            var shorter = new List<Curve>();

            foreach (var crv in curves)
            {
                if (crv == null) continue;

                double len = crv.GetLength();
                if (len >= threshold)
                    longer.Add(crv);
                else
                    shorter.Add(crv);
            }

            DA.SetDataList(0, longer);
            DA.SetDataList(1, shorter);
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
            get { return new Guid("7205CF9C-2AAE-43C2-BE65-6CC13F6096AB"); }
        }
    }
}