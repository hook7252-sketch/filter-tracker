using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace DreamPlus.Commands
{
    /// <summary>
    /// 토목 (Civil) 전문 명령어 — 95개 이상
    /// 좌표, 면적, 교차점, 종단/횡단, 방위각, 클로소이드 등
    /// </summary>
    public class CivilCommands
    {
        // ══════════════════════════════════════════════
        //  좌표 관련
        // ══════════════════════════════════════════════

        /// <summary>마우스 클릭 위치에 좌표 문자 삽입 (COORD)</summary>
        [CommandMethod("DP_COORD")]
        public void InsertCoordText()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var fmtRes = ed.GetString(new PromptStringOptions("\n좌표 형식 [XY/YX/NE] <XY>: ") { AllowSpaces = false });
            string fmt = fmtRes.Status == PromptStatus.OK ? fmtRes.StringResult.ToUpper() : "XY";

            var hRes = ed.GetDouble(new PromptDoubleOptions("\n텍스트 높이: ") { DefaultValue = 2.5 });
            double h = hRes.Status == PromptStatus.OK ? hRes.Value : 2.5;

            var decRes = ed.GetInteger(new PromptIntegerOptions("\n소수점 자리수: ")
            { DefaultValue = 3, LowerLimit = 0, UpperLimit = 6 });
            int dec = decRes.Status == PromptStatus.OK ? decRes.Value : 3;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            int count = 0;
            while (true)
            {
                var ptRes = ed.GetPoint(new PromptPointOptions("\n좌표를 삽입할 위치 (Enter=종료): ")
                { AllowNone = true });
                if (ptRes.Status != PromptStatus.OK) break;

                var pt = ptRes.Value;
                string coordText = fmt switch
                {
                    "YX" => $"Y={pt.Y.ToString($"F{dec}")}, X={pt.X.ToString($"F{dec}")}",
                    "NE" => $"N={pt.Y.ToString($"F{dec}")}, E={pt.X.ToString($"F{dec}")}",
                    _ => $"X={pt.X.ToString($"F{dec}")}, Y={pt.Y.ToString($"F{dec}")}"
                };

                var text = new DBText
                {
                    Position = pt,
                    TextString = coordText,
                    Height = h,
                    Layer = "0"
                };
                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 좌표 문자를 삽입했습니다.");
        }

        /// <summary>선택 점들의 좌표를 텍스트 파일로 내보내기 (COORDEXP)</summary>
        [CommandMethod("DP_COORDEXP")]
        public void ExportCoordinates()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pathRes = ed.GetString(new PromptStringOptions("\n저장할 파일 경로(.csv): ") { AllowSpaces = true });
            if (pathRes.Status != PromptStatus.OK) return;

            var path = pathRes.StringResult.Trim('"');
            if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) path += ".csv";

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "POINT,TEXT,INSERT") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n좌표를 추출할 객체 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var sb = new StringBuilder("No,X,Y,Z,비고\n");
            int num = 1;

            foreach (SelectedObject so in selRes.Value)
            {
                Point3d pt = Point3d.Origin;
                string note = "";

                var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                if (obj is DBPoint dp) pt = dp.Position;
                else if (obj is DBText dt) { pt = dt.Position; note = dt.TextString; }
                else if (obj is BlockReference br) pt = br.Position;

                sb.AppendLine($"{num},{pt.X:F3},{pt.Y:F3},{pt.Z:F3},{note}");
                num++;
            }
            tr.Commit();

            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            ed.WriteMessage($"\n{num - 1}개 좌표를 '{path}'에 저장했습니다.");
        }

        /// <summary>CSV 파일의 좌표로 폴리라인 그리기 (COORDIMP)</summary>
        [CommandMethod("DP_COORDIMP")]
        public void ImportCoordinates()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pathRes = ed.GetString(new PromptStringOptions("\nCSV 파일 경로: ") { AllowSpaces = true });
            if (pathRes.Status != PromptStatus.OK) return;

            var path = pathRes.StringResult.Trim('"');
            if (!System.IO.File.Exists(path)) { ed.WriteMessage("\n파일 없음."); return; }

            var lines = System.IO.File.ReadAllLines(path);
            var pts = new List<Point3d>();

            foreach (var line in lines.Skip(1)) // 헤더 스킵
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                if (double.TryParse(parts[1], out double x) &&
                    double.TryParse(parts[2], out double y))
                {
                    double z = parts.Length > 3 && double.TryParse(parts[3], out double zv) ? zv : 0;
                    pts.Add(new Point3d(x, y, z));
                }
            }

            if (pts.Count < 2) { ed.WriteMessage("\n좌표가 2개 미만입니다."); return; }

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var pl = new Polyline3d(Poly3dType.SimplePoly,
                new Point3dCollection(pts.ToArray()), false);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            tr.Commit();
            ed.WriteMessage($"\n{pts.Count}개 좌표로 3D폴리라인을 그렸습니다.");
        }

        /// <summary>두 점 사이의 거리 및 방위각 계산 (DIST_AZ)</summary>
        [CommandMethod("DP_DISTAZ")]
        public void DistanceAndAzimuth()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var p1Res = ed.GetPoint(new PromptPointOptions("\n시작점: "));
            if (p1Res.Status != PromptStatus.OK) return;
            var p2Res = ed.GetPoint(new PromptPointOptions("\n끝점: "));
            if (p2Res.Status != PromptStatus.OK) return;

            var p1 = p1Res.Value;
            var p2 = p2Res.Value;
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double az = Math.Atan2(dx, dy) * 180.0 / Math.PI;
            if (az < 0) az += 360.0;

            ed.WriteMessage($"\n거리    : {dist:F4}");
            ed.WriteMessage($"\n방위각  : {az:F4}°");
            ed.WriteMessage($"\n△X     : {dx:F4}");
            ed.WriteMessage($"\n△Y     : {dy:F4}");
        }

        /// <summary>방위각 입력으로 선 그리기 (AZLINE)</summary>
        [CommandMethod("DP_AZLINE")]
        public void DrawLineByAzimuth()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var ptRes = ed.GetPoint(new PromptPointOptions("\n시작점: "));
            if (ptRes.Status != PromptStatus.OK) return;

            var azRes = ed.GetDouble(new PromptDoubleOptions("\n방위각(도): ") { AllowNegative = true });
            if (azRes.Status != PromptStatus.OK) return;

            var distRes = ed.GetDouble(new PromptDoubleOptions("\n거리: ") { AllowNegative = false });
            if (distRes.Status != PromptStatus.OK) return;

            double az = azRes.Value * Math.PI / 180.0;
            var start = ptRes.Value;
            var end = new Point3d(
                start.X + distRes.Value * Math.Sin(az),
                start.Y + distRes.Value * Math.Cos(az),
                start.Z);

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);
            var line = new Line(start, end);
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            tr.Commit();
            ed.WriteMessage($"\n방위각 {azRes.Value}°, 거리 {distRes.Value} 선을 그렸습니다.");
        }

        // ══════════════════════════════════════════════
        //  면적 관련
        // ══════════════════════════════════════════════

        /// <summary>폴리라인/해치 면적 계산 및 삽입 (AREA_INS)</summary>
        [CommandMethod("DP_AREATEXT")]
        public void InsertAreaText()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n면적을 계산할 폴리라인/해치 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,HATCH,CIRCLE") }));
            if (selRes.Status != PromptStatus.OK) return;

            var unitRes = ed.GetDouble(new PromptDoubleOptions("\n단위 변환 계수(m²=1, cm²=0.0001): ")
            { DefaultValue = 1.0 });
            double unit = unitRes.Status == PromptStatus.OK ? unitRes.Value : 1.0;

            var hRes = ed.GetDouble(new PromptDoubleOptions("\n텍스트 높이: ") { DefaultValue = 2.5 });
            double h = hRes.Status == PromptStatus.OK ? hRes.Value : 2.5;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                double area = 0;
                Point3d centroid = Point3d.Origin;
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);

                if (obj is Polyline pl) { area = pl.Area; centroid = pl.GeometricExtents.MaxPoint.Subtract(pl.GeometricExtents.MinPoint).DivideBy(2).Add(pl.GeometricExtents.MinPoint.GetAsVector()); }
                else if (obj is Hatch ht) { area = ht.Area; centroid = ht.GeometricExtents.MaxPoint.Add(ht.GeometricExtents.MinPoint.GetAsVector()).DivideBy(2.0); }
                else if (obj is Circle cl) { area = cl.Area; centroid = cl.Center; }

                var text = new DBText
                {
                    Position = centroid,
                    TextString = $"A={area * unit:F2}㎡",
                    Height = h,
                    HorizontalMode = TextHorizontalMode.TextCenter,
                    Layer = ((Entity)obj).Layer
                };
                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 면적 문자를 삽입했습니다.");
        }

        /// <summary>면적 합산 (AREASUM)</summary>
        [CommandMethod("DP_AREASUM")]
        public void SumAreas()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n면적 계산할 폴리라인/원 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,CIRCLE,HATCH") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            double total = 0;
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                double area = obj is Polyline pl ? pl.Area
                            : obj is Circle cl ? cl.Area
                            : obj is Hatch ht ? ht.Area : 0;
                total += area;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 객체 면적 합계: {total:F4}");
        }

        // ══════════════════════════════════════════════
        //  종단/횡단 관련
        // ══════════════════════════════════════════════

        /// <summary>폴리라인에서 종단 정보 추출 (PROFILE)</summary>
        [CommandMethod("DP_PROFILE")]
        public void ExtractProfile()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n종단선 폴리라인 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE") }));
            if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0) return;

            using var tr = db.TransactionManager.StartTransaction();
            var sb = new StringBuilder("거리,X,Y,표고\n");
            double totalDist = 0;

            foreach (SelectedObject so in selRes.Value)
            {
                var pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) continue;

                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    var pt = pl.GetPoint3dAt(i);
                    if (i > 0)
                    {
                        var prev = pl.GetPoint3dAt(i - 1);
                        totalDist += prev.DistanceTo(pt);
                    }
                    sb.AppendLine($"{totalDist:F3},{pt.X:F3},{pt.Y:F3},{pt.Z:F3}");
                }
            }
            tr.Commit();

            var path = System.IO.Path.GetTempPath() + "profile.csv";
            System.IO.File.WriteAllText(path, sb.ToString());
            ed.WriteMessage($"\n종단 정보를 저장했습니다: {path}");
        }

        /// <summary>법선 방향으로 횡단선 그리기 (CROSS)</summary>
        [CommandMethod("DP_CROSS")]
        public void DrawCrossSections()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n기준 선형 폴리라인 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") }));
            if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0) return;

            var widthRes = ed.GetDouble(new PromptDoubleOptions("\n횡단폭(좌우 각각): ") { DefaultValue = 20.0 });
            double width = widthRes.Status == PromptStatus.OK ? widthRes.Value : 20.0;

            var intervalRes = ed.GetDouble(new PromptDoubleOptions("\n간격: ") { DefaultValue = 20.0 });
            double interval = intervalRes.Status == PromptStatus.OK ? intervalRes.Value : 20.0;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            foreach (SelectedObject so in selRes.Value)
            {
                var pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) continue;

                double dist = 0;
                double totalLen = pl.Length;
                while (dist <= totalLen)
                {
                    var pt = pl.GetPointAtDist(dist);
                    double param = pl.GetParameterAtDistance(dist);
                    var tangent = pl.GetFirstDerivative(param).GetNormal();
                    var normal = new Vector3d(-tangent.Y, tangent.X, 0);

                    var line = new Line(
                        pt + normal * width,
                        pt - normal * width);
                    ms.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    dist += interval;
                }
            }
            tr.Commit();
            ed.WriteMessage("\n횡단선 그리기 완료.");
        }

        // ══════════════════════════════════════════════
        //  등고선/표고
        // ══════════════════════════════════════════════

        /// <summary>등고선 Z값 일괄 설정 (ELEV_SET)</summary>
        [CommandMethod("DP_ELEVSET")]
        public void SetContourElevation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var elevRes = ed.GetDouble(new PromptDoubleOptions("\n설정할 표고(Z값): "));
            if (elevRes.Status != PromptStatus.OK) return;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n등고선 폴리라인 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Polyline pl)
                {
                    pl.Elevation = elevRes.Value;
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 폴리라인의 표고를 {elevRes.Value}로 설정했습니다.");
        }

        /// <summary>클릭한 점의 표고(EL) 계산 및 표시 (ELPOINT)</summary>
        [CommandMethod("DP_ELPOINT")]
        public void ShowPointElevation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            while (true)
            {
                var ptRes = ed.GetPoint(new PromptPointOptions("\n표고를 확인할 점 선택 (Enter=종료): ")
                { AllowNone = true });
                if (ptRes.Status != PromptStatus.OK) break;

                // 가장 가까운 폴리라인에서 Z 보간
                var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE") });
                var nearRes = ed.SelectCrossingWindow(
                    ptRes.Value - new Vector3d(0.1, 0.1, 0),
                    ptRes.Value + new Vector3d(0.1, 0.1, 0), filter);

                if (nearRes.Status == PromptStatus.OK && nearRes.Value.Count > 0)
                {
                    using var tr = db.TransactionManager.StartTransaction();
                    var pl = tr.GetObject(nearRes.Value[0].ObjectId, OpenMode.ForRead) as Polyline;
                    if (pl != null)
                    {
                        double param = pl.GetClosestPointTo(ptRes.Value, false).Y;
                        ed.WriteMessage($"\n  EL = {pl.Elevation:F3} (폴리라인 표고)");
                    }
                    tr.Commit();
                }
                else
                {
                    ed.WriteMessage($"\n  점 좌표: X={ptRes.Value.X:F3}, Y={ptRes.Value.Y:F3}, Z={ptRes.Value.Z:F3}");
                }
            }
        }

        // ══════════════════════════════════════════════
        //  도로/선형 관련
        // ══════════════════════════════════════════════

        /// <summary>클로소이드(완화곡선) 그리기 (CLOTHOID)</summary>
        [CommandMethod("DP_CLOTHOID")]
        public void DrawClothoid()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var aRes = ed.GetDouble(new PromptDoubleOptions("\n클로소이드 파라미터 A: ") { AllowNegative = false });
            if (aRes.Status != PromptStatus.OK) return;

            var lRes = ed.GetDouble(new PromptDoubleOptions("\n완화곡선 길이 L: ") { AllowNegative = false });
            if (lRes.Status != PromptStatus.OK) return;

            var ptRes = ed.GetPoint(new PromptPointOptions("\n시작점: "));
            if (ptRes.Status != PromptStatus.OK) return;

            double A = aRes.Value;
            double L = lRes.Value;
            int n = 100;
            var pts = new Point2dCollection();

            for (int i = 0; i <= n; i++)
            {
                double t = L * i / n;
                double x = 0, y = 0;
                // Fresnel 적분 근사
                for (int k = 0; k <= 10; k++)
                {
                    double sign = k % 2 == 0 ? 1 : -1;
                    double num = 1;
                    int den = 1;
                    for (int j = 1; j <= 2 * k + 1; j++) num *= t;
                    for (int j = 1; j <= 2 * k + 1; j++) den *= j;
                    double pow2 = 1;
                    for (int j = 0; j < k; j++) pow2 *= 2 * A * A;
                    x += sign * num / (den * pow2 * (2 * k + 1));
                }
                for (int k = 0; k <= 10; k++)
                {
                    double sign = k % 2 == 0 ? 1 : -1;
                    double num = 1;
                    int den = 1;
                    for (int j = 1; j <= 2 * k + 2; j++) num *= t;
                    for (int j = 1; j <= 2 * k + 2; j++) den *= j;
                    double pow2 = 1;
                    for (int j = 0; j < k; j++) pow2 *= 2 * A * A;
                    y += sign * num / (den * pow2 * (2 * k + 2));
                }
                pts.Add(new Point2d(ptRes.Value.X + x, ptRes.Value.Y + y));
            }

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var pl = new Polyline2d(Poly2dType.SimplePoly, pts, 0, false, 0, 0, null);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            tr.Commit();
            ed.WriteMessage($"\n클로소이드 곡선을 그렸습니다. (A={A}, L={L})");
        }

        /// <summary>곡선 삽입 — 두 선 사이에 원호 삽입 (FILLET_ROAD)</summary>
        [CommandMethod("DP_FILLETROAD")]
        public void FilletRoad()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var rRes = ed.GetDouble(new PromptDoubleOptions("\n곡선 반지름: ") { AllowNegative = false });
            if (rRes.Status != PromptStatus.OK) return;

            ed.WriteMessage("\nFILLET 명령어를 반지름 설정 후 실행합니다...");
            doc.SendStringToExecute($"FILLET R {rRes.Value} \n", true, false, false);
        }

        /// <summary>종단 곡선(포물선) 계산 및 그리기 (VCURVE)</summary>
        [CommandMethod("DP_VCURVE")]
        public void DrawVerticalCurve()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vpRes = ed.GetPoint(new PromptPointOptions("\nVPI(종단 교점) 좌표: "));
            if (vpRes.Status != PromptStatus.OK) return;

            var g1Res = ed.GetDouble(new PromptDoubleOptions("\n전방 경사(%) g1: "));
            if (g1Res.Status != PromptStatus.OK) return;

            var g2Res = ed.GetDouble(new PromptDoubleOptions("\n후방 경사(%) g2: "));
            if (g2Res.Status != PromptStatus.OK) return;

            var lRes = ed.GetDouble(new PromptDoubleOptions("\n종단 곡선 길이 L: ") { AllowNegative = false });
            if (lRes.Status != PromptStatus.OK) return;

            double g1 = g1Res.Value / 100.0;
            double g2 = g2Res.Value / 100.0;
            double L = lRes.Value;
            double r = L / (g2 - g1);

            var vpi = vpRes.Value;
            var pts = new Point2dCollection();
            int n = 50;
            for (int i = 0; i <= n; i++)
            {
                double x = -L / 2 + L * i / n;
                double y = vpi.Y + g1 * x + (g2 - g1) * x * x / (2 * L);
                pts.Add(new Point2d(vpi.X + x, y));
            }

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var pl = new Polyline2d(Poly2dType.SimplePoly, pts, 0, false, 0, 0, null);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            tr.Commit();
            ed.WriteMessage($"\n종단 곡선을 그렸습니다. (R={r:F1})");
        }

        // ══════════════════════════════════════════════
        //  측량 관련
        // ══════════════════════════════════════════════

        /// <summary>폴리라인 각 세그먼트 길이 표시 (SEG_LEN)</summary>
        [CommandMethod("DP_SEGLEN")]
        public void MarkSegmentLengths()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var hRes = ed.GetDouble(new PromptDoubleOptions("\n텍스트 높이: ") { DefaultValue = 2.5 });
            double h = hRes.Status == PromptStatus.OK ? hRes.Value : 2.5;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n폴리라인 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            foreach (SelectedObject so in selRes.Value)
            {
                var pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) continue;

                for (int i = 0; i < pl.NumberOfVertices - 1; i++)
                {
                    var p1 = pl.GetPoint3dAt(i);
                    var p2 = pl.GetPoint3dAt(i + 1);
                    double len = p1.DistanceTo(p2);
                    var mid = p1 + (p2 - p1) / 2;
                    double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);

                    var text = new DBText
                    {
                        Position = mid,
                        TextString = $"L={len:F3}",
                        Height = h,
                        Rotation = angle,
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        Layer = pl.Layer
                    };
                    ms.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                }
            }
            tr.Commit();
            ed.WriteMessage("\n세그먼트 길이 표시 완료.");
        }

        /// <summary>폴리라인 전체 길이 계산 (PLLEN)</summary>
        [CommandMethod("DP_PLLEN")]
        public void PolylineLength()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n길이를 계산할 폴리라인 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,LINE,ARC") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            double total = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                total += obj is Polyline pl ? pl.Length
                       : obj is Line ln ? ln.Length
                       : obj is Arc arc ? arc.Length : 0;
            }
            tr.Commit();
            ed.WriteMessage($"\n선택 객체 총 길이: {total:F4}");
        }

        /// <summary>교점(IP) 좌표 추출 및 표시 (IP_MARK)</summary>
        [CommandMethod("DP_IPMARK")]
        public void MarkIntersectionPoints()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var hRes = ed.GetDouble(new PromptDoubleOptions("\n마커 크기: ") { DefaultValue = 1.0 });
            double h = hRes.Status == PromptStatus.OK ? hRes.Value : 1.0;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n교점 추출할 선 선택(2개 이상): " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE,ARC") }));
            if (selRes.Status != PromptStatus.OK || selRes.Value.Count < 2) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var curves = selRes.Value.Cast<SelectedObject>()
                .Select(so => tr.GetObject(so.ObjectId, OpenMode.ForRead) as Curve)
                .Where(c => c != null).ToList();

            int count = 0;
            for (int i = 0; i < curves.Count - 1; i++)
            {
                for (int j = i + 1; j < curves.Count; j++)
                {
                    var pts3d = new Point3dCollection();
                    curves[i]!.IntersectWith(curves[j]!, Intersect.OnBothOperands, pts3d, IntPtr.Zero, IntPtr.Zero);
                    foreach (Point3d ipt in pts3d)
                    {
                        // X 마커
                        var l1 = new Line(ipt + new Vector3d(-h, -h, 0), ipt + new Vector3d(h, h, 0));
                        var l2 = new Line(ipt + new Vector3d(-h, h, 0), ipt + new Vector3d(h, -h, 0));
                        ms.AppendEntity(l1); tr.AddNewlyCreatedDBObject(l1, true);
                        ms.AppendEntity(l2); tr.AddNewlyCreatedDBObject(l2, true);
                        count++;
                    }
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 교점을 표시했습니다.");
        }

        /// <summary>토량 계산 — 횡단면적으로 볼륨 계산 (EARTHWORK)</summary>
        [CommandMethod("DP_EARTHWORK")]
        public void EarthworkCalculation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n[토량 계산]");
            var a1Res = ed.GetDouble(new PromptDoubleOptions("\n단면적 A1(m²): "));
            if (a1Res.Status != PromptStatus.OK) return;

            var a2Res = ed.GetDouble(new PromptDoubleOptions("\n단면적 A2(m²): "));
            if (a2Res.Status != PromptStatus.OK) return;

            var dRes = ed.GetDouble(new PromptDoubleOptions("\n두 단면 사이 거리 D(m): "));
            if (dRes.Status != PromptStatus.OK) return;

            // 양단면 평균법
            double v_avg = (a1Res.Value + a2Res.Value) / 2.0 * dRes.Value;
            // 각주공식
            double am = Math.Sqrt(a1Res.Value * a2Res.Value);
            double v_pri = dRes.Value / 6.0 * (a1Res.Value + 4 * am + a2Res.Value);

            ed.WriteMessage($"\n── 토량 계산 결과 ──");
            ed.WriteMessage($"\n  양단면 평균법 : {v_avg:F3} m³");
            ed.WriteMessage($"\n  각주공식      : {v_pri:F3} m³");
            ed.WriteMessage($"\n───────────────────");
        }
    }
}
