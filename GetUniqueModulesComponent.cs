using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Mantis
{
    public class GetUniqueModulesComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GetUniqueModulesComponent class.
        /// </summary>
        public GetUniqueModulesComponent()
          : base("Get Unique Modules", "UniqueModules",
      "Find unique geometry modules based on plane orientation and area",
      "Mantis", "Analysis")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Geometry", "G", "Brep geometry to analyze for uniqueness", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Decimal Places", "D", "Decimal places for area rounding", GH_ParamAccess.item, 6);
            pManager.AddPointParameter("Reference Point", "R", "Reference point for sorting modules by distance", GH_ParamAccess.item, Point3d.Origin);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Modules", "M", "Unique module geometry", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Number of Modules", "N", "Total number of unique modules found", GH_ParamAccess.item);
            pManager.AddTextParameter("Labels", "L", "Labels for each unique module", GH_ParamAccess.list);
            pManager.AddGenericParameter("Label Dots", "LD", "TextDot labels positioned on modules", GH_ParamAccess.list);
            pManager.AddTextParameter("Plane Groups", "PG", "Plane group identifiers for each unique module", GH_ParamAccess.list);
            pManager.AddNumberParameter("Unique Areas", "UA", "Areas of unique modules only in square meters", GH_ParamAccess.list);
            pManager.AddNumberParameter("All Areas", "AA", "Areas of all input geometry in square meters", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> breps = new List<Brep>();
            int decimalPlaces = 6;
            Point3d referencePoint = Point3d.Origin;

            if (!DA.GetDataList(0, breps)) return;
            if (!DA.GetData(1, ref decimalPlaces)) return;
            if (!DA.GetData(2, ref referencePoint)) return;

            Message = "Filter By Plane and Area";

            List<double> allAreasInSquareMeters = new List<double>();
            Dictionary<string, Dictionary<double, ModuleData>> planeGroupedBreps = new Dictionary<string, Dictionary<double, ModuleData>>();
            List<Plane> knownPlanes = new List<Plane>();
            List<string> planeLabels = new List<string>();

            double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            foreach (Brep brep in breps)
            {
                if (brep != null && brep.IsValid)
                {
                    // Find the largest surface in the polysurface
                    BrepFace largestFace = null;
                    double maxArea = 0;

                    foreach (BrepFace face in brep.Faces)
                    {
                        AreaMassProperties areaProps = AreaMassProperties.Compute(face);
                        if (areaProps != null)
                        {
                            double area = areaProps.Area;
                            if (area > maxArea)
                            {
                                maxArea = area;
                                largestFace = face;
                            }
                        }
                    }

                    if (largestFace != null)
                    {
                        // Calculate area in square meters
                        double areaMm = maxArea;
                        double areaM = areaMm / 1e6;
                        double roundedAreaM = Math.Round(areaM, decimalPlaces);
                        allAreasInSquareMeters.Add(roundedAreaM);

                        // Get the plane of the largest face
                        Plane facePlane;
                        if (largestFace.TryGetPlane(out facePlane))
                        {
                            // Find or create plane group using coplanar checking
                            string planeGroup = GetPlaneGroup(facePlane, knownPlanes, planeLabels, tolerance);

                            // Initialize plane group if it doesn't exist
                            if (!planeGroupedBreps.ContainsKey(planeGroup))
                            {
                                planeGroupedBreps[planeGroup] = new Dictionary<double, ModuleData>();
                            }

                            // Only add if this area doesn't exist for this plane group (making it unique)
                            if (!planeGroupedBreps[planeGroup].ContainsKey(roundedAreaM))
                            {
                                Brep duplicatedFace = largestFace.DuplicateFace(false);
                                Point3d center = duplicatedFace.GetBoundingBox(true).Center;
                                double distanceToRef = center.DistanceTo(referencePoint);

                                planeGroupedBreps[planeGroup][roundedAreaM] = new ModuleData
                                {
                                    Brep = duplicatedFace,
                                    Center = center,
                                    DistanceToReference = distanceToRef,
                                    Area = roundedAreaM
                                };
                            }
                        }
                        else
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not extract plane from face - skipping geometry");
                        }
                    }
                }
            }

            // Create a list of all modules with their data for sorting
            List<(string planeGroup, double area, ModuleData data)> allModules = new List<(string, double, ModuleData)>();

            foreach (var planeGroupPair in planeGroupedBreps)
            {
                string groupName = planeGroupPair.Key;
                foreach (var areaPair in planeGroupPair.Value)
                {
                    allModules.Add((groupName, areaPair.Key, areaPair.Value));
                }
            }

            // Sort by plane group first, then by distance to reference point within each group
            allModules.Sort((a, b) =>
            {
                int planeComparison = string.Compare(a.planeGroup, b.planeGroup);
                if (planeComparison != 0) return planeComparison;
                return a.data.DistanceToReference.CompareTo(b.data.DistanceToReference);
            });

            // Extract sorted data and create labels
            List<Brep> uniqueModules = new List<Brep>();
            List<string> moduleGroups = new List<string>();
            List<double> uniqueAreas = new List<double>();
            List<string> labels = new List<string>();
            List<TextDot> labelDots = new List<TextDot>();

            // Track numbering within each plane group
            Dictionary<string, int> planeGroupCounters = new Dictionary<string, int>();

            foreach (var module in allModules)
            {
                string planeGroup = module.planeGroup;

                // Initialize counter for this plane group if not exists
                if (!planeGroupCounters.ContainsKey(planeGroup))
                {
                    planeGroupCounters[planeGroup] = 0;
                }

                // Create label: PlaneGroup-Number (e.g., "Plane_A-0", "Plane_A-1", etc.)
                string label = $"{planeGroup}-{planeGroupCounters[planeGroup]}";
                planeGroupCounters[planeGroup]++;

                uniqueModules.Add(module.data.Brep);
                moduleGroups.Add(planeGroup);
                uniqueAreas.Add(module.area);
                labels.Add(label);

                // Create TextDot at the center of the module
                labelDots.Add(new TextDot(label, module.data.Center));
            }

            // Set outputs
            DA.SetDataList(0, uniqueModules);           // Modules
            DA.SetData(1, uniqueModules.Count);         // Number of Modules
            DA.SetDataList(2, labels);                  // Labels
            DA.SetDataList(3, labelDots);               // Label Dots
            DA.SetDataList(4, moduleGroups);            // Plane Groups
            DA.SetDataList(5, uniqueAreas);             // Unique Areas
            DA.SetDataList(6, allAreasInSquareMeters);  // All Areas
        }

        // Helper data structure to store module information
        private class ModuleData
        {
            public Brep Brep { get; set; }
            public Point3d Center { get; set; }
            public double DistanceToReference { get; set; }
            public double Area { get; set; }
        }

        // Helper method to get plane group using coplanar checking
        private string GetPlaneGroup(Plane candidatePlane, List<Plane> knownPlanes, List<string> planeLabels, double tolerance)
        {
            // Check if this plane is coplanar with any known planes
            for (int i = 0; i < knownPlanes.Count; i++)
            {
                if (AreCoplanar(candidatePlane, knownPlanes[i], tolerance))
                {
                    return planeLabels[i];
                }
            }

            // If not coplanar with any known plane, create a new group
            string newLabel = $"Plane_{(char)('A' + knownPlanes.Count)}";
            knownPlanes.Add(candidatePlane);
            planeLabels.Add(newLabel);
            return newLabel;
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
            get { return new Guid("DD162CFB-9E97-45C2-A5D7-5C241B102DAB"); }
        }
    }
}