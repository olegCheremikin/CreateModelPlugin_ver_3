using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateModelPlugin_ver_3
{
    [TransactionAttribute(TransactionMode.Manual)]

    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Level level1 = SelectLevel(doc, "Уровень 1");
            Level level2 = SelectLevel(doc, "Уровень 2");

            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);

            double dx = width / 2;
            double dy = depth / 2;

            Transaction ts = new Transaction(doc, "Построение модели");
            ts.Start();
            {
                List<Wall> walls = CreateWall(dx, dy, level1, level2, doc); 
                AddDoor(doc, level1, walls[1]);           
                AddWindow(doc, level1, walls[0]);           
                AddWindow(doc, level1, walls[2]);          
                AddWindow(doc, level1, walls[3]);           
                AddRoоf(doc, level2, walls, width, depth);		

            }
            ts.Commit();

            return Result.Succeeded;
        }

        private void AddRoоf(Document doc, Level level2, List<Wall> walls, double width, double depth)
        {
            
                RoofType roofType = new FilteredElementCollector(doc)  
                    .OfClass(typeof(RoofType))
                    .OfType<RoofType>()
                    .Where(x => x.Name.Equals("Типовой - 125мм"))
                    .Where(x => x.FamilyName.Equals("Базовая крыша"))
                    .FirstOrDefault();

                View view = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .OfType<View>()
                    .Where(x => x.Name.Equals("Уровень 1"))
                    .FirstOrDefault();

                double wallWight = walls[0].Width;
                double dt = wallWight / 2;

                double extrusionStart = -width / 2 - dt; 
                double extrusionEnd = width / 2 + dt;
                double curveStart = -depth / 2 - dt;
                double curveEnd = +depth / 2 + dt;

                CurveArray curveArray = new CurveArray();           
                curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level2.Elevation), new XYZ(0, 0, level2.Elevation + 10)));
                curveArray.Append(Line.CreateBound(new XYZ(0, 0, level2.Elevation + 10), new XYZ(0, curveEnd, level2.Elevation)));

                ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), view);
                ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, extrusionStart, extrusionEnd);
                extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;
        }

        private void AddWindow(Document doc, Level level1, Wall wall)                
        {
            FamilySymbol windowType = new FilteredElementCollector(doc) 
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Windows)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name == "0915 x 1830 мм")
                 .Where(x => x.Family.Name == "Фиксированные")
                 .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;  
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);		
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);		
            XYZ point = (point1 + point2) / 2;				

            if (!windowType.IsActive)				
            {
                windowType.Activate();
            }

            FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);    

            window.flipFacing();  		// повернуть окно

            double height = UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);
            window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(height);  
        }

        private void AddDoor(Document doc, Level level1, Wall wall)         
        {
            FamilySymbol doorType = new FilteredElementCollector(doc) 
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name == "0915 x 2134 мм")
                 .Where(x => x.Family.Name == "Одиночные-Щитовые")
                 .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;  
            XYZ point1 = hostCurve.Curve.GetEndPoint(0); 		        
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);		        
            XYZ point = (point1 + point2) / 2;				            

            if (!doorType.IsActive)				                        
            {
                doorType.Activate();
            }
            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural); 
        }

        public Level SelectLevel(Document doc, string levelName)  
        {
            List<Level> listLevels = new FilteredElementCollector(doc)  
                        .OfClass(typeof(Level))
                         .OfType<Level>()
                          .ToList();
            Level SelectLevel = listLevels
                            .Where(x => x.Name.Equals(levelName))       
                            .OfType<Level>()
                            .FirstOrDefault();
            return SelectLevel;
        }

        public List<Wall> CreateWall(double dx, double dy, Level lowLevel, Level highLevel, Document doc) 
        {
            List<Wall> walls = new List<Wall>();                    
            List<XYZ> points = new List<XYZ>();                     

            for (int i = 0; i < 4; i++)
            {
                points.Add(new XYZ(-dx, -dy, 0));
                points.Add(new XYZ(dx, -dy, 0));
                points.Add(new XYZ(dx, dy, 0));
                points.Add(new XYZ(-dx, dy, 0));
                points.Add(new XYZ(-dx, -dy, 0));
                Line line = Line.CreateBound(points[i], points[i + 1]);   
                Wall wall = Wall.Create(doc, line, lowLevel.Id, false);   
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(highLevel.Id);   
                walls.Add(wall);                                        
            }
            return walls;
        }

    }
}
