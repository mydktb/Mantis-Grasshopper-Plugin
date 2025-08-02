using System;
using System.Collections.Generic;
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class PathBuilderComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CurveFramesComponent class.
        /// </summary>
        public PathBuilderComponent()
        : base("Path Builder", "PathBuilder",
              "Generates frames along a curve, orients profiles, and creates lofted geometry",
              "Mantis", "Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Profile", "P", "Profile curve to orient along the main curve", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Profile Source", "PS", "Source plane for profile orientation", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddCurveParameter("Curve", "C", "Input curve to generate frames along", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Number of frames to generate", GH_ParamAccess.item, 10);
            pManager.AddBooleanParameter("Force Z", "F", "Force Z-axis to world Z-axis", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Cap", "Cap", "Cap the lofted geometry", GH_ParamAccess.item, false);

            // Make profile optional
            pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Frames", "P", "Generated frames along curve", GH_ParamAccess.list);
            pManager.AddLineParameter("X Axis", "X", "X-axis direction lines", GH_ParamAccess.list);
            pManager.AddLineParameter("Y Axis", "Y", "Y-axis direction lines", GH_ParamAccess.list);
            pManager.AddLineParameter("Z Axis", "Z", "Z-axis direction lines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Oriented Profiles", "OP", "Profile curves oriented to each frame", GH_ParamAccess.list);
            pManager.AddBrepParameter("Lofted Geometry", "LG", "Lofted brep geometry from oriented profiles", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input variables
            Curve profile = null;
            Plane profileSource = Plane.WorldXY;
            Curve curve = null;
            int count = 10;
            bool forceZaxis = true;
            bool cap = false;

            // Get inputs
            DA.GetData(0, ref profile); // Optional
            if (!DA.GetData(1, ref profileSource)) return;
            if (!DA.GetData(2, ref curve)) return;
            if (!DA.GetData(3, ref count)) return;
            if (!DA.GetData(4, ref forceZaxis)) return;
            if (!DA.GetData(5, ref cap)) return;

            // Validate inputs
            if (curve == null || !curve.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid input curve");
                return;
            }

            if (count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Count must be at least 2");
                return;
            }

            // Initialize output lists
            var planeList = new List<Plane>();
            var xLines = new List<Line>();
            var yLines = new List<Line>();
            var zLines = new List<Line>();
            var orientedProfiles = new List<Curve>();
            Brep loftedGeometry = null;

            try
            {
                // Calculate curve length and step
                double totalLength = curve.GetLength();
                double step = totalLength / (count - 1);
                double axisLength = totalLength * 0.05;

                // Generate frames along the curve
                for (int i = 0; i < count; i++)
                {
                    double t;
                    if (!curve.LengthParameter(i * step, out t))
                        continue;

                    Point3d origin = curve.PointAt(t);
                    Plane p;

                    if (forceZaxis)
                    {
                        // Construct plane with Z-axis pointing upward
                        Vector3d tangent = curve.TangentAt(t);
                        tangent.Unitize();

                        Vector3d worldUp = Vector3d.ZAxis;
                        if (Math.Abs(Vector3d.Multiply(tangent, worldUp)) > 0.999)
                            worldUp = Vector3d.YAxis;

                        Vector3d xAxis = tangent;
                        Vector3d yAxis = Vector3d.CrossProduct(worldUp, xAxis);
                        yAxis.Unitize();
                        Vector3d zAxis = Vector3d.CrossProduct(xAxis, yAxis);
                        zAxis.Unitize();

                        p = new Plane(origin, xAxis, yAxis);
                    }
                    else
                    {
                        // Use natural perpendicular frame from curve
                        if (!curve.FrameAt(t, out p))
                            continue;
                    }

                    planeList.Add(p);

                    // Create axis visualization lines
                    xLines.Add(new Line(origin, origin + p.XAxis * axisLength));
                    yLines.Add(new Line(origin, origin + p.YAxis * axisLength));
                    zLines.Add(new Line(origin, origin + p.ZAxis * axisLength));

                    // Orient profile if provided
                    if (profile != null && profile.IsValid)
                    {
                        // Create transformation from profile source plane to frame plane
                        Transform orient = Transform.PlaneToPlane(profileSource, p);

                        // Duplicate and transform the profile
                        Curve orientedProfile = profile.DuplicateCurve();
                        orientedProfile.Transform(orient);
                        orientedProfiles.Add(orientedProfile);
                    }
                }

                // Create lofted geometry if we have oriented profiles
                if (orientedProfiles.Count > 1)
                {
                    var loftResult = Brep.CreateFromLoft(orientedProfiles, Point3d.Unset, Point3d.Unset,
                        LoftType.Normal, false);

                    if (loftResult != null && loftResult.Length > 0)
                    {
                        loftedGeometry = loftResult[0];

                        // Cap the geometry if requested
                        if (cap && loftedGeometry != null)
                        {
                            var cappedBrep = loftedGeometry.CapPlanarHoles(0.01);
                            if (cappedBrep != null)
                                loftedGeometry = cappedBrep;
                        }
                    }
                }
                else if (orientedProfiles.Count == 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Need at least 2 oriented profiles for lofting");
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error generating frames: {ex.Message}");
                return;
            }

            // Set outputs
            DA.SetDataList(0, planeList);
            DA.SetDataList(1, xLines);
            DA.SetDataList(2, yLines);
            DA.SetDataList(3, zLines);
            DA.SetDataList(4, orientedProfiles);
            DA.SetData(5, loftedGeometry);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add an image file to your project resources and access it like this:
                // return Resources.IconForThisComponent;
                //var iBytes = Properties.Resources.PathBuilderIcon;
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
            get { return new Guid("4FFCB638-53C7-4EB1-92ED-E5AE25CAF2CB"); }
        }
    }
}