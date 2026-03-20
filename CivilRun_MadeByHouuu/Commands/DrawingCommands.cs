using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    /// <summary>
    /// 도면 마킹/배지 명령어
    /// DP_BY : 변경있음 배지
    /// DP_BN : 변경없음 배지
    /// </summary>
    public class DrawingCommands
    {
        [CommandMethod("DP_BY")]
        public void BadgeYes() => CreateBadge("변경있음", 10, "DP_BY");

        [CommandMethod("DP_BN")]
        public void BadgeNo()  => CreateBadge("변경없음",  7, "DP_BN");

        // ── 내부 헬퍼 ──────────────────────────────────

        private static void CreateBadge(string text, short colorIndex, string label)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database; var ed = doc.Editor;
            Application.SetSystemVariable("LWDISPLAY", 1);

            using var tr = db.TransactionManager.StartTransaction();

            EnsureLayer(tr, db, "BB_TAG", colorIndex);

            var p1Res = ed.GetPoint($"\n[{label}] 첫 번째 끝점 지정: ");
            if (p1Res.Status != PromptStatus.OK) return;

            var p2o = new PromptPointOptions($"\n[{label}] 반대 끝점 지정: ")
                { UseBasePoint = true, BasePoint = p1Res.Value };
            var p2Res = ed.GetCorner(p2o);
            if (p2Res.Status != PromptStatus.OK) return;

            var p1 = p1Res.Value; var p2 = p2Res.Value;
            double minx = Math.Min(p1.X, p2.X), miny = Math.Min(p1.Y, p2.Y);
            double maxx = Math.Max(p1.X, p2.X), maxy = Math.Max(p1.Y, p2.Y);
            double w = maxx - minx, h = maxy - miny;
            if (w < 1e-6 || h < 1e-6) return;

            // 우상단 기준 박스 크기 계산
            double prx = maxx - w / 14.0, pry = maxy - h / 14.0;
            double bx1 = prx - w * 0.1035, by1 = pry - h * 0.0483;
            double cx  = (bx1 + prx) / 2.0,  cy  = (by1 + pry) / 2.0;

            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            var acColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                              Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);

            // 폴리선 박스
            var pl = new Polyline();
            pl.SetDatabaseDefaults(); pl.Layer = "BB_TAG"; pl.Color = acColor;
            pl.LineWeight = LineWeight.LineWeight070;
            pl.AddVertexAt(0, new Point2d(bx1, by1), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(prx, by1), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(prx, pry), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(bx1, pry), 0, 0, 0);
            pl.Closed = true;
            space.AppendEntity(pl); tr.AddNewlyCreatedDBObject(pl, true);

            // 중앙 문자
            var mt = new MText();
            mt.SetDatabaseDefaults(); mt.Layer = "BB_TAG"; mt.Color = acColor;
            mt.Location   = new Point3d(cx, cy, 0);
            mt.TextHeight = (pry - by1) * 0.45;
            mt.Width      = prx - bx1;
            mt.Attachment = AttachmentPoint.MiddleCenter;
            mt.Contents   = text;
            space.AppendEntity(mt); tr.AddNewlyCreatedDBObject(mt, true);

            tr.Commit();
        }

        private static void EnsureLayer(Transaction tr, Database db, string name, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(name))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord
                {
                    Name = name,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex),
                    LinetypeObjectId = db.ContinuousLinetype
                };
                lt.Add(ltr); tr.AddNewlyCreatedDBObject(ltr, true);
            }
            else
            {
                var ltr = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForWrite);
                if (ltr.IsOff)    ltr.IsOff    = false;
                if (ltr.IsFrozen) ltr.IsFrozen = false;
                if (ltr.IsLocked) ltr.IsLocked = false;
            }
        }
    }
}
