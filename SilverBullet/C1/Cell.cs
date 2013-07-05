using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.C1
{
    public class Cell
    {
        public int? Id { get; set; }
        public string PlateId { get; set; }
        public string Well { get; set; }
        public double Diameter { get; set; }
        public double Area { get; set; }
        public string Type { get; set; }
        public string imgPath { get; set; }

        public Cell(int? id, string plateId, string well, double diameter, double area, string type, string imgPath)
        {
            this.Id = id;
            this.PlateId = plateId;
            this.Well = well;
            this.Diameter = diameter;
            this.Area = area;
            this.Type = type;
            this.imgPath = imgPath;
        }
    }
}
