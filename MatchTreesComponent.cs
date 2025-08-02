using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Mantis
{
    public class MatchTreesComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MatchTreesComponent class.
        /// </summary>
        public MatchTreesComponent()
          : base("Match Trees", "MatchTree",
              "Match the structure of one data tree to another by redistributing items",
              "Mantis", "Data")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Structure Tree", "S", "Data tree that provides the target structure", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Data Tree", "D", "Data tree containing items to redistribute", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Cycle", "C", "Cycle through data items when structure is larger", GH_ParamAccess.item, false);

            // Make cycle parameter optional
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Matched Tree", "MT", "Data tree with items redistributed to match structure", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Items Used", "IU", "Number of items used from data tree", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Items Available", "IA", "Total number of items available in data tree", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> structureTree = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> dataTree = new GH_Structure<IGH_Goo>();
            bool cycle = false;

            // Get input data
            if (!DA.GetDataTree(0, out structureTree)) return;
            if (!DA.GetDataTree(1, out dataTree)) return;
            DA.GetData(2, ref cycle);

            // Input validation
            if (structureTree == null || dataTree == null)
            {
                DA.SetDataTree(0, new GH_Structure<IGH_Goo>());
                DA.SetData(1, 0);
                DA.SetData(2, 0);
                return;
            }

            // Create a new DataTree to store the result
            GH_Structure<IGH_Goo> matchedTree = new GH_Structure<IGH_Goo>();

            // Flatten all items from dataTree into a single list
            List<IGH_Goo> allItems = new List<IGH_Goo>();
            foreach (GH_Path path in dataTree.Paths)
            {
                var branch = dataTree.get_Branch(path);
                foreach (IGH_Goo item in branch)
                {
                    allItems.Add(item);
                }
            }

            int totalItems = allItems.Count;
            int itemsUsed = 0;

            if (totalItems == 0)
            {
                // If no items available, output empty structure
                DA.SetDataTree(0, matchedTree);
                DA.SetData(1, 0);
                DA.SetData(2, 0);
                return;
            }

            // Create an index to track position in the flattened list
            int currentIndex = 0;

            // Fill the matchedTree with the structure of structureTree, using items from dataTree
            foreach (GH_Path path in structureTree.Paths)
            {
                List<IGH_Goo> newBranch = new List<IGH_Goo>();
                var structureBranch = structureTree.get_Branch(path);
                int count = structureBranch.Count;

                for (int i = 0; i < count; i++)
                {
                    if (currentIndex < totalItems)
                    {
                        newBranch.Add(allItems[currentIndex]);
                        currentIndex++;
                        itemsUsed++;
                    }
                    else if (cycle && totalItems > 0)
                    {
                        // Cycle back to beginning
                        currentIndex = currentIndex % totalItems;
                        newBranch.Add(allItems[currentIndex]);
                        currentIndex++;
                        itemsUsed++;
                    }
                    else
                    {
                        break; // No more items to assign and cycling is disabled
                    }
                }

                if (newBranch.Count > 0)
                {
                    matchedTree.AppendRange(newBranch, path);
                }
            }

            // Set output data
            DA.SetDataTree(0, matchedTree);
            DA.SetData(1, itemsUsed);
            DA.SetData(2, totalItems);
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
            get { return new Guid("0A83B182-89BE-499F-A1D9-B877A641052A"); }
        }
    }
}