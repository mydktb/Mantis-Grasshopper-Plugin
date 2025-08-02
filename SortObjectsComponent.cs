using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mantis
{
    public class SortObjectsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SortObjectsComponent class.
        /// </summary>
        public SortObjectsComponent()
          : base("Sort Objects", "SortObj",
              "Sort geometry objects by various methods (X, Y, Z, distance, grid, angle)",
              "Mantis", "Analysis")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Objects", "O", "List of geometry objects to sort", GH_ParamAccess.list);
            pManager.AddTextParameter("Sort Method", "M", "Sort method: X, Y, Z, Distance, Grid, Grid-BT-LR, Angle", GH_ParamAccess.item, "X");
            pManager.AddPointParameter("Reference Point", "P", "Reference point for distance and angle sorting", GH_ParamAccess.item, Point3d.Origin);

            // Make sort method and reference point optional
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Sorted Objects", "SO", "Objects sorted by the specified method", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Sorted Indices", "SI", "Original indices of the sorted objects", GH_ParamAccess.list);
            pManager.AddPointParameter("Centroids", "C", "Centroids of the objects used for sorting", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> objects = new List<GeometryBase>();
            string sortMethod = "X";
            Point3d refPoint = Point3d.Origin;

            // Get input data
            if (!DA.GetDataList(0, objects)) return;
            DA.GetData(1, ref sortMethod);
            DA.GetData(2, ref refPoint);

            // Input validation
            if (objects == null || objects.Count == 0)
            {
                DA.SetDataList(0, new List<GeometryBase>());
                DA.SetDataList(1, new List<int>());
                DA.SetDataList(2, new List<Point3d>());
                return;
            }

            // Create a list of objects with their original indices and centroids
            var objectData = new List<(GeometryBase obj, int index, Point3d centroid)>();

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] == null) continue;
                Point3d centroid = GetCentroid(objects[i]);
                objectData.Add((objects[i], i, centroid));
            }

            // Sort based on the specified method
            switch (sortMethod.ToLower())
            {
                case "x":
                case "left-right":
                    objectData.Sort((a, b) => a.centroid.X.CompareTo(b.centroid.X));
                    break;

                case "y":
                case "bottom-top":
                    objectData.Sort((a, b) => a.centroid.Y.CompareTo(b.centroid.Y));
                    break;

                case "z":
                case "back-front":
                    objectData.Sort((a, b) => a.centroid.Z.CompareTo(b.centroid.Z));
                    break;

                case "distance":
                case "radial":
                    objectData.Sort((a, b) =>
                    {
                        double distA = a.centroid.DistanceTo(refPoint);
                        double distB = b.centroid.DistanceTo(refPoint);
                        return distA.CompareTo(distB);
                    });
                    break;

                case "grid":
                case "grid-lr-bt":
                    // Sort by Y first (rows), then by X (columns)
                    objectData.Sort((a, b) =>
                    {
                        int yCompare = a.centroid.Y.CompareTo(b.centroid.Y);
                        if (yCompare != 0) return yCompare;
                        return a.centroid.X.CompareTo(b.centroid.X);
                    });
                    break;

                case "grid-bt-lr":
                    // Sort by X first (columns), then by Y (rows)
                    objectData.Sort((a, b) =>
                    {
                        int xCompare = a.centroid.X.CompareTo(b.centroid.X);
                        if (xCompare != 0) return xCompare;
                        return a.centroid.Y.CompareTo(b.centroid.Y);
                    });
                    break;

                case "angle":
                    // Sort by angle from reference point
                    objectData.Sort((a, b) =>
                    {
                        Vector3d vecA = a.centroid - refPoint;
                        Vector3d vecB = b.centroid - refPoint;
                        double angleA = Math.Atan2(vecA.Y, vecA.X);
                        double angleB = Math.Atan2(vecB.Y, vecB.X);
                        return angleA.CompareTo(angleB);
                    });
                    break;

                default:
                    // Default to X sorting
                    objectData.Sort((a, b) => a.centroid.X.CompareTo(b.centroid.X));
                    break;
            }

            // Extract sorted objects, indices, and centroids
            var sortedObjects = objectData.Select(x => x.obj).ToList();
            var sortedIndices = objectData.Select(x => x.index).ToList();
            var centroids = objectData.Select(x => x.centroid).ToList();

            // Set output data
            DA.SetDataList(0, sortedObjects);
            DA.SetDataList(1, sortedIndices);
            DA.SetDataList(2, centroids);
        }

        /// <summary>
        /// Helper method to get centroid of different geometry types
        /// </summary>
        private Point3d GetCentroid(GeometryBase obj)
        {
            switch (obj)
            {
                case Rhino.Geometry.Point point:
                    return point.Location;

                case Curve curve:
                    return curve.PointAtNormalizedLength(0.5);

                case Surface surface:
                    var domain = surface.Domain(0);
                    var domain2 = surface.Domain(1);
                    return surface.PointAt(domain.Mid, domain2.Mid);

                case Brep brep:
                    return brep.GetBoundingBox(true).Center;

                case Mesh mesh:
                    return mesh.GetBoundingBox(true).Center;

                default:
                    // Fallback to bounding box center
                    return obj.GetBoundingBox(true).Center;
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
            get { return new Guid("BD334C39-DE53-4A91-854A-64C205D81E01"); }
        }
    }
}