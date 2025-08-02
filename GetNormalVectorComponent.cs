using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Mantis
{
    public class GetNormalVectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GetNormalVectorComponent class.
        /// </summary>
        public GetNormalVectorComponent()
           : base("Get Normal Vector", "GetNormal",
              "Extract normal vectors from geometry with visualization lines",
              "Mantis", "Analysis")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Geometry to analyze", GH_ParamAccess.list);
            pManager.AddNumberParameter("Amplitude", "A", "Scale factor for normal vectors", GH_ParamAccess.item, 1.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddVectorParameter("Normal Vectors", "N", "Extracted normal vectors", GH_ParamAccess.list);
            pManager.AddVectorParameter("Z Vectors", "Z", "Reference Z vectors", GH_ParamAccess.list);
            pManager.AddLineParameter("Vector Lines", "L", "Visualization lines for normals and Z vectors", GH_ParamAccess.list);
            pManager.AddPointParameter("Base Points", "P", "Base points where normals are computed", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> geometry = new List<GeometryBase>();
            double amplitude = 1.0;

            if (!DA.GetDataList(0, geometry)) return;
            if (!DA.GetData(1, ref amplitude)) return;

            List<Vector3d> normals = new List<Vector3d>();
            List<Vector3d> zVecs = new List<Vector3d>();
            List<Line> visualLines = new List<Line>();
            List<Point3d> basePoints = new List<Point3d>();

            // Create base Z vector (always pointing up)
            Vector3d baseZVector = new Vector3d(0, 0, 1) * amplitude;

            foreach (GeometryBase geo in geometry)
            {
                Vector3d normal = Vector3d.Zero;
                Point3d basePoint = Point3d.Origin;

                // Handle different geometry types
                if (geo is Surface)
                {
                    Surface srf = geo as Surface;
                    double u = srf.Domain(0).Mid;
                    double v = srf.Domain(1).Mid;
                    normal = srf.NormalAt(u, v);
                    basePoint = srf.PointAt(u, v);
                }
                else if (geo is Brep)
                {
                    Brep brep = geo as Brep;
                    if (brep.Faces.Count > 0)
                    {
                        BrepFace face = brep.Faces[0];
                        double u = face.Domain(0).Mid;
                        double v = face.Domain(1).Mid;
                        normal = face.NormalAt(u, v);
                        basePoint = face.PointAt(u, v);
                    }
                    else
                    {
                        basePoint = brep.GetBoundingBox(true).Center;
                    }
                }
                else if (geo is Curve)
                {
                    Curve crv = geo as Curve;
                    double t = crv.Domain.Mid;
                    basePoint = crv.PointAt(t);

                    if (crv.IsClosed)
                    {
                        Brep[] breps = Brep.CreatePlanarBreps(crv, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (breps != null && breps.Length > 0)
                        {
                            BrepFace face = breps[0].Faces[0];
                            double u = face.Domain(0).Mid;
                            double v = face.Domain(1).Mid;
                            normal = face.NormalAt(u, v);
                            basePoint = face.PointAt(u, v);
                        }
                        else
                        {
                            normal = new Vector3d(0, 0, 1);
                        }
                    }
                    else
                    {
                        Vector3d tangent = crv.TangentAt(t);
                        Vector3d worldZ = new Vector3d(0, 0, 1);
                        normal = Vector3d.CrossProduct(tangent, worldZ);

                        if (normal.Length < 0.001)
                        {
                            Vector3d worldX = new Vector3d(1, 0, 0);
                            normal = Vector3d.CrossProduct(tangent, worldX);
                        }
                    }
                }
                else if (geo is Mesh)
                {
                    Mesh mesh = geo as Mesh;
                    basePoint = mesh.GetBoundingBox(true).Center;

                    if (mesh.Faces.Count > 0)
                    {
                        if (mesh.FaceNormals.Count == 0)
                        {
                            mesh.FaceNormals.ComputeFaceNormals();
                        }

                        if (mesh.FaceNormals.Count > 0)
                        {
                            normal = mesh.FaceNormals[0];
                        }
                        else
                        {
                            normal = new Vector3d(0, 0, 1);
                        }
                    }
                    else
                    {
                        normal = new Vector3d(0, 0, 1);
                    }
                }

                // Ensure normal is valid
                if (normal.Length > 0.001)
                {
                    normal.Unitize();

                    // Make sure normal points upward (positive Z component)
                    if (normal.Z < 0)
                    {
                        normal.Reverse();
                    }

                    // Scale by amplitude
                    normal *= amplitude;
                }
                else
                {
                    // Fallback to Z vector if normal calculation fails
                    normal = baseZVector;
                }

                // Add to output lists
                normals.Add(normal);
                zVecs.Add(baseZVector);
                basePoints.Add(basePoint);

                // Create visualization lines
                // Z vector line (reference)
                Line zLine = new Line(basePoint, basePoint + baseZVector);
                visualLines.Add(zLine);

                // Normal vector line
                Line normalLine = new Line(basePoint, basePoint + normal);
                visualLines.Add(normalLine);
            }

            // Set outputs
            DA.SetDataList(0, normals);
            DA.SetDataList(1, zVecs);
            DA.SetDataList(2, visualLines);
            DA.SetDataList(3, basePoints);
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
            get { return new Guid("CAE3E8BF-368B-4889-BA52-5D99BEB9C4D1"); }
        }
    }
}