using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    public class BlockCommands
    {
        // ──────────────────────────────────────────────
        // DP_BLOCK_INSERT : 외부 블록 삽입 (DWG 파일 → 블록)
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_INSERT")]
        public void InsertBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pathRes = ed.GetString(new PromptStringOptions("\nDWG 파일 경로: ") { AllowSpaces = true });
            if (pathRes.Status != PromptStatus.OK) return;

            var path = pathRes.StringResult.Trim('"');
            if (!File.Exists(path))
            {
                ed.WriteMessage($"\n파일을 찾을 수 없습니다: {path}");
                return;
            }

            var blockName = Path.GetFileNameWithoutExtension(path);

            // 외부 DWG를 블록으로 가져오기
            using (var sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(path, FileShare.Read, true, null);
                db.Insert(blockName, sourceDb, true);
            }

            // 삽입 위치 지정
            var ptRes = ed.GetPoint(new PromptPointOptions($"\n'{blockName}' 블록 삽입 위치: "));
            if (ptRes.Status != PromptStatus.OK) return;

            var scaleRes = ed.GetDouble(new PromptDoubleOptions("\n축척(기본값 1.0): ") { DefaultValue = 1.0, AllowNone = true });
            double scale = scaleRes.Status == PromptStatus.OK ? scaleRes.Value : 1.0;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var br = new BlockReference(ptRes.Value, ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[blockName])
            {
                ScaleFactors = new Scale3d(scale)
            };

            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            tr.Commit();

            ed.WriteMessage($"\n'{blockName}' 블록을 삽입했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_LIST : 도면 내 블록 목록 출력
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_LIST")]
        public void ListBlocks()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            ed.WriteMessage("\n──────────────── 블록 목록 ────────────────");
            int count = 0;
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsAnonymous || btr.IsLayout) continue;
                ed.WriteMessage($"\n  {btr.Name}");
                count++;
            }
            ed.WriteMessage($"\n총 {count}개의 블록\n────────────────────────────────────────");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_COUNT : 선택한 블록 개수 세기
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_COUNT")]
        public void CountBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n세어볼 블록 이름: ") { AllowSpaces = false });
            if (nameRes.Status != PromptStatus.OK) return;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.BlockName, nameRes.StringResult),
            });

            var selRes = ed.SelectAll(filter);
            int count = selRes.Status == PromptStatus.OK ? selRes.Value.Count : 0;
            ed.WriteMessage($"\n'{nameRes.StringResult}' 블록: {count}개");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_EXPLODE_ALL : 선택 영역 블록 전체 분해
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_EXPLODE_ALL")]
        public void ExplodeAllBlocks()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n분해할 블록 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "INSERT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (so == null) continue;
                var br = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as BlockReference;
                if (br == null) continue;

                var exploded = new DBObjectCollection();
                br.Explode(exploded);
                foreach (DBObject obj in exploded)
                {
                    var ent = (Entity)obj;
                    ms.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                }
                br.Erase();
                count++;
            }

            tr.Commit();
            ed.WriteMessage($"\n{count}개의 블록을 분해했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_REPLACE : 블록 이름 일괄 교체
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_REPLACE")]
        public void ReplaceBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var oldRes = ed.GetString(new PromptStringOptions("\n교체할 블록 이름: "));
            if (oldRes.Status != PromptStatus.OK) return;

            var newRes = ed.GetString(new PromptStringOptions("\n새 블록 이름: "));
            if (newRes.Status != PromptStatus.OK) return;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.BlockName, oldRes.StringResult),
            });

            var selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) { ed.WriteMessage("\n대상 블록이 없습니다."); return; }

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(newRes.StringResult))
            {
                ed.WriteMessage($"\n'{newRes.StringResult}' 블록이 존재하지 않습니다.");
                return;
            }

            var newId = bt[newRes.StringResult];
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var br = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as BlockReference;
                if (br == null) continue;
                br.BlockTableRecord = newId;
                count++;
            }

            tr.Commit();
            ed.WriteMessage($"\n{count}개의 블록을 '{newRes.StringResult}'으로 교체했습니다.");
        }

        // ══════════════════════════════════════════════
        //  DP_CU  — 원+번호 연속 복제
        // ══════════════════════════════════════════════

        private static int s_cuCounter = 1;

        /// <summary>견본 원+숫자를 클릭 위치마다 연속 복제하며 자동 번호 증가</summary>
        [CommandMethod("CU", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void CU()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database; var ed = doc.Editor;

            var psr = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n견본이 될 원과 숫자(문자)를 선택하세요: " });
            if (psr.Status != PromptStatus.OK) return;

            ObjectId templateCircleId = ObjectId.Null, templateTextId = ObjectId.Null;
            Vector3d offset   = Vector3d.Zero;
            int baseNumber    = 0;
            bool isMText      = false, isDBTextAligned = false;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                Point3d circleCenter = Point3d.Origin, textAnchor = Point3d.Origin;
                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity; if (ent == null) continue;
                    if (ent is Circle c && templateCircleId.IsNull)
                    { templateCircleId = ent.ObjectId; circleCenter = c.Center; }
                    else if (ent is DBText dt && templateTextId.IsNull)
                    {
                        if (!int.TryParse(dt.TextString, out baseNumber)) { ed.WriteMessage("\n숫자 문자를 선택하세요."); return; }
                        templateTextId = ent.ObjectId;
                        isDBTextAligned = dt.HorizontalMode != TextHorizontalMode.TextLeft || dt.VerticalMode != TextVerticalMode.TextBase;
                        textAnchor = isDBTextAligned ? dt.AlignmentPoint : dt.Position;
                    }
                    else if (ent is MText mt && templateTextId.IsNull)
                    {
                        if (!int.TryParse(mt.Contents, out baseNumber)) { ed.WriteMessage("\n숫자 문자를 선택하세요."); return; }
                        templateTextId = ent.ObjectId; isMText = true; textAnchor = mt.Location;
                    }
                }
                if (templateTextId.IsNull) { ed.WriteMessage("\n숫자 문자를 찾을 수 없습니다."); return; }
                if (!templateCircleId.IsNull) offset = textAnchor - circleCenter;
                s_cuCounter = baseNumber + 1;
                tr.Commit();
            }

            while (true)
            {
                var ppo = new PromptPointOptions("\n다음 위치 클릭 (ESC=종료): ") { AllowNone = true };
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) break;

                using var tr = db.TransactionManager.StartTransaction();
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                if (!templateCircleId.IsNull)
                {
                    var oldC  = (Circle)tr.GetObject(templateCircleId, OpenMode.ForRead);
                    var newC  = (Circle)oldC.Clone(); newC.Center = ppr.Value;
                    space.AppendEntity(newC); tr.AddNewlyCreatedDBObject(newC, true);
                }

                var oldText = (Entity)tr.GetObject(templateTextId, OpenMode.ForRead);
                var newText = (Entity)oldText.Clone();
                var anchor  = ppr.Value + offset;
                if (isMText)
                { ((MText)newText).Contents = s_cuCounter.ToString(); ((MText)newText).Location = anchor; }
                else
                {
                    ((DBText)newText).TextString = s_cuCounter.ToString();
                    if (isDBTextAligned) ((DBText)newText).AlignmentPoint = anchor;
                    else                ((DBText)newText).Position        = anchor;
                }
                space.AppendEntity(newText); tr.AddNewlyCreatedDBObject(newText, true);
                s_cuCounter++;
                tr.Commit();
            }
        }

        // ══════════════════════════════════════════════
        //  DP_XYB — 비균일 XY 스케일 (WinForms 다이얼로그)
        // ══════════════════════════════════════════════

        /// <summary>X/Y 축별 다른 스케일 적용 — 측정 보조 포함</summary>
        [CommandMethod("XYB", CommandFlags.Modal)]
        public void XYB()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database; var ed = doc.Editor;

            LoadScaleFromDoc(db, out double sx, out double sy);

            bool? explode = null;
            while (explode == null)
            {
                using var frm = new XybForm(sx, sy);
                var r = frm.ShowDialog();
                if (r == System.Windows.Forms.DialogResult.Cancel) return;
                if (r == System.Windows.Forms.DialogResult.Retry)
                {
                    if (frm.Request == "MeasureX") { if (MeasureAxis(ed, "X", out double s)) sx = s; }
                    else                           { if (MeasureAxis(ed, "Y", out double s)) sy = s; }
                    continue;
                }
                if (!frm.TryGet(out sx, out sy)) continue;
                explode = frm.CbExplode;
            }

            SaveScaleToDoc(db, sx, sy);
            ApplyNonUniformScale(ed, db, sx, sy, explode!.Value);
            ed.WriteMessage($"\n완료: X={sx:G17}, Y={sy:G17}");
        }

        // ── XYB 내부 헬퍼 ─────────────────────────────

        private static bool MeasureAxis(Editor ed, string axis, out double scale)
        {
            scale = 1.0;
            var axVec = axis == "X"
                ? ed.CurrentUserCoordinateSystem.CoordinateSystem3d.Xaxis.GetNormal()
                : ed.CurrentUserCoordinateSystem.CoordinateSystem3d.Yaxis.GetNormal();

            var p1 = ed.GetPoint($"\n{axis}축 기준 길이 1번째 점: "); if (p1.Status != PromptStatus.OK) return false;
            var p2o = new PromptPointOptions($"\n{axis}축 기준 길이 2번째 점: ") { BasePoint = p1.Value, UseBasePoint = true };
            var p2 = ed.GetPoint(p2o); if (p2.Status != PromptStatus.OK) return false;
            var p3 = ed.GetPoint($"\n{axis}축 목표 길이 1번째 점: "); if (p3.Status != PromptStatus.OK) return false;
            var p4o = new PromptPointOptions($"\n{axis}축 목표 길이 2번째 점: ") { BasePoint = p3.Value, UseBasePoint = true };
            var p4 = ed.GetPoint(p4o); if (p4.Status != PromptStatus.OK) return false;

            double baseLen = Math.Abs((p2.Value - p1.Value).DotProduct(axVec));
            double targLen = Math.Abs((p4.Value - p3.Value).DotProduct(axVec));
            if (baseLen < 1e-9) { ed.WriteMessage("\n기준 길이가 0입니다."); return false; }
            scale = targLen / baseLen;
            ed.WriteMessage($"\n{axis}축 스케일 = {scale:G17}");
            return true;
        }

        private static void ApplyNonUniformScale(Editor ed, Database db, double sx, double sy, bool explode)
        {
            var psr = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n스케일 적용할 객체 선택: " });
            if (psr.Status != PromptStatus.OK) return;
            var pr = ed.GetPoint("\n기준점 선택: ");
            if (pr.Status != PromptStatus.OK) return;

            ObjectId bdefId = ObjectId.Null, brId = ObjectId.Null;
            var originalIds = new System.Collections.Generic.List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt    = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead); bt.UpgradeOpen();
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var bdef  = new BlockTableRecord { Name = "*U" };
                bdefId = bt.Add(bdef); tr.AddNewlyCreatedDBObject(bdef, true);

                var toLocal = Matrix3d.Displacement(Point3d.Origin - pr.Value);
                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue; originalIds.Add(so.ObjectId);
                    var src   = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                    var clone = (Entity)src.Clone(); clone.TransformBy(toLocal);
                    bdef.AppendEntity(clone); tr.AddNewlyCreatedDBObject(clone, true);
                }

                var br = new BlockReference(pr.Value, bdefId) { ScaleFactors = new Scale3d(sx, sy, 1.0) };
                brId = space.AppendEntity(br); tr.AddNewlyCreatedDBObject(br, true);

                foreach (var oid in originalIds)
                    ((Entity)tr.GetObject(oid, OpenMode.ForWrite)).Erase(true);

                if (explode)
                {
                    var brRef = (BlockReference)tr.GetObject(brId, OpenMode.ForRead);
                    var exploded = new DBObjectCollection(); brRef.Explode(exploded);
                    foreach (DBObject obj in exploded)
                    { if (obj is Entity e) { space.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true); } obj.Dispose(); }
                    brRef.UpgradeOpen(); brRef.Erase(true);
                }
                tr.Commit();
            }

            if (explode && !bdefId.IsNull)
                try { using var tr = db.TransactionManager.StartTransaction(); var bd = (BlockTableRecord)tr.GetObject(bdefId, OpenMode.ForWrite); if (!bd.IsErased) bd.Erase(true); tr.Commit(); } catch { }
        }

        private static void SaveScaleToDoc(Database db, double sx, double sy)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            Xrecord xr;
            if (nod.Contains("DLT_Scale"))
                xr = (Xrecord)tr.GetObject(nod.GetAt("DLT_Scale"), OpenMode.ForWrite);
            else
            { nod.UpgradeOpen(); xr = new Xrecord(); nod.SetAt("DLT_Scale", xr); tr.AddNewlyCreatedDBObject(xr, true); }
            xr.Data = new ResultBuffer(new TypedValue((int)DxfCode.Real, sx), new TypedValue((int)DxfCode.Real, sy));
            tr.Commit();
        }

        private static void LoadScaleFromDoc(Database db, out double sx, out double sy)
        {
            sx = 1.0; sy = 1.0;
            using var tr = db.TransactionManager.StartTransaction();
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains("DLT_Scale")) { tr.Commit(); return; }
            var xr   = (Xrecord)tr.GetObject(nod.GetAt("DLT_Scale"), OpenMode.ForRead);
            var data = xr.Data?.AsArray();
            if (data?.Length >= 2 && data[0].TypeCode == (int)DxfCode.Real && data[1].TypeCode == (int)DxfCode.Real)
            { sx = (double)data[0].Value; sy = (double)data[1].Value; }
            tr.Commit();
        }
    }

    // ══════════════════════════════════════════════
    //  XybForm — DP_XYB 에서 사용하는 WinForms 다이얼로그
    // ══════════════════════════════════════════════
    internal sealed class XybForm : System.Windows.Forms.Form
    {
        private readonly System.Windows.Forms.TextBox _tbX, _tbY;
        private readonly System.Windows.Forms.CheckBox _cbLink, _cbExplode;

        public string? Request { get; private set; }
        public bool    CbExplode => _cbExplode.Checked;

        public XybForm(double sx = 1.0, double sy = 1.0)
        {
            Text = "XY 비균일 스케일"; FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false; ClientSize = new System.Drawing.Size(290, 160); TopMost = true;

            void L(string t, int x, int y) { var l = new System.Windows.Forms.Label { Text = t, Left = x, Top = y, Width = 55 }; Controls.Add(l); }
            System.Windows.Forms.TextBox TB(int x, int y, string v) { var tb = new System.Windows.Forms.TextBox { Left = x, Top = y, Width = 120, Text = v }; Controls.Add(tb); return tb; }
            System.Windows.Forms.Button BTN(string t, int x, int y, System.Windows.Forms.DialogResult dr, Action? extra = null)
            {
                var b = new System.Windows.Forms.Button { Text = t, Left = x, Top = y, Width = 60 };
                b.Click += (_, __) => { extra?.Invoke(); DialogResult = dr; };
                Controls.Add(b); return b;
            }

            L("X 축척:", 12, 16); _tbX = TB(72, 12, sx.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            BTN("측정", 200, 10, System.Windows.Forms.DialogResult.Retry, () => Request = "MeasureX");

            L("Y 축척:", 12, 46); _tbY = TB(72, 42, sy.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            BTN("측정", 200, 40, System.Windows.Forms.DialogResult.Retry, () => Request = "MeasureY");

            _cbLink    = new System.Windows.Forms.CheckBox { Text = "Y=X (동일)", Left = 12, Top = 74, Width = 110 };
            _cbExplode = new System.Windows.Forms.CheckBox { Text = "Explode 후 남김", Left = 130, Top = 74, Width = 150 };
            _cbLink.CheckedChanged += (_, __) => { if (_cbLink.Checked) _tbY.Text = _tbX.Text; };
            _tbX.TextChanged       += (_, __) => { if (_cbLink.Checked) _tbY.Text = _tbX.Text; };
            Controls.Add(_cbLink); Controls.Add(_cbExplode);

            var ok     = BTN("확인", 60,  110, System.Windows.Forms.DialogResult.OK);
            var cancel = BTN("취소", 160, 110, System.Windows.Forms.DialogResult.Cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        public bool TryGet(out double sx, out double sy)
        {
            sx = 1; sy = 1;
            return double.TryParse(_tbX.Text, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out sx) && sx > 0
                && double.TryParse(_tbY.Text, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out sy) && sy > 0;
        }
    }
}
