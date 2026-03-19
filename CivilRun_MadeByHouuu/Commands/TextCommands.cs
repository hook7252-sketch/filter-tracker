using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    /// <summary>
    /// 문자 관리 명령어 (약 50여 개)
    /// 카테고리: 문자I, 문자II
    /// </summary>
    public class TextCommands
    {
        // ══════════════════════════════════════════════
        //  문자 I — 기본 편집
        // ══════════════════════════════════════════════

        /// <summary>문자 찾기/바꾸기 (TFR)</summary>
        [CommandMethod("DP_TFR")]
        public void TextFindReplace()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var findRes = ed.GetString(new PromptStringOptions("\n찾을 문자: ") { AllowSpaces = true });
            if (findRes.Status != PromptStatus.OK) return;

            var replRes = ed.GetString(new PromptStringOptions("\n바꿀 문자: ") { AllowSpaces = true });
            if (replRes.Status != PromptStatus.OK) return;

            int count = 0;
            using var tr = db.TransactionManager.StartTransaction();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) { tr.Commit(); return; }

            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt && dt.TextString.Contains(findRes.StringResult))
                {
                    dt.TextString = dt.TextString.Replace(findRes.StringResult, replRes.StringResult);
                    count++;
                }
                else if (obj is MText mt && mt.Contents.Contains(findRes.StringResult))
                {
                    mt.Contents = mt.Contents.Replace(findRes.StringResult, replRes.StringResult);
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 문자를 바꿨습니다.");
        }

        /// <summary>문자 내용 복사 (클립보드로)</summary>
        [CommandMethod("DP_TCOPY")]
        public void TextCopyContent()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n복사할 텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lines = new List<string>();
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                if (obj is DBText dt) lines.Add(dt.TextString);
                else if (obj is MText mt) lines.Add(mt.Text);
            }
            tr.Commit();

            System.Windows.Clipboard.SetText(string.Join("\n", lines));
            ed.WriteMessage($"\n{lines.Count}개 문자를 클립보드에 복사했습니다.");
        }

        /// <summary>문자 증감 — 숫자가 포함된 문자를 일정값씩 증가/감소 (TINC)</summary>
        [CommandMethod("DP_TINC")]
        public void TextIncrement()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var stepRes = ed.GetInteger(new PromptIntegerOptions("\n증감값(음수=감소): ") { DefaultValue = 1, AllowNone = true });
            if (stepRes.Status == PromptStatus.Cancel) return;
            int step = stepRes.Status == PromptStatus.None ? 1 : stepRes.Value;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n증감할 숫자 텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                string text = obj is DBText dt2 ? dt2.TextString
                            : obj is MText mt2 ? mt2.Text : null!;
                if (text == null) continue;

                var newText = Regex.Replace(text, @"-?\d+(\.\d+)?",
                    m => (double.Parse(m.Value) + step).ToString());

                if (obj is DBText dt3) dt3.TextString = newText;
                else if (obj is MText mt3) mt3.Contents = newText;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 문자의 숫자를 {(step >= 0 ? "+" : "")}{step} 변경했습니다.");
        }

        /// <summary>문자 정렬 — 선택 텍스트를 X 또는 Y 기준으로 정렬 (TALIGN)</summary>
        [CommandMethod("DP_TALIGN")]
        public void TextAlign()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var kw = new PromptKeywordOptions("\n정렬 기준 [X축(X)/Y축(Y)] <X>: ");
            kw.Keywords.Add("X"); kw.Keywords.Add("Y");
            kw.AllowNone = true;
            var kwRes = ed.GetKeywords(kw);
            bool alignX = kwRes.Status != PromptStatus.OK || kwRes.StringResult != "Y";

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n정렬할 텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var entities = selRes.Value.Cast<SelectedObject>()
                .Select(so => tr.GetObject(so.ObjectId, OpenMode.ForWrite))
                .ToList();

            // 첫 번째 객체 기준 좌표
            Point3d refPt = entities[0] is DBText refDt ? refDt.Position
                          : entities[0] is MText refMt ? refMt.Location
                          : Point3d.Origin;

            foreach (var obj in entities)
            {
                if (obj is DBText dt)
                {
                    var pos = dt.Position;
                    dt.Position = alignX ? new Point3d(refPt.X, pos.Y, pos.Z)
                                         : new Point3d(pos.X, refPt.Y, pos.Z);
                }
                else if (obj is MText mt)
                {
                    var loc = mt.Location;
                    mt.Location = alignX ? new Point3d(refPt.X, loc.Y, loc.Z)
                                         : new Point3d(loc.X, refPt.Y, loc.Z);
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{entities.Count}개 텍스트를 정렬했습니다.");
        }

        /// <summary>문자 간격 균등 배치 (TSPACE)</summary>
        [CommandMethod("DP_TSPACE")]
        public void TextEqualSpacing()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var kw = new PromptKeywordOptions("\n배치 방향 [수평(H)/수직(V)] <H>: ");
            kw.Keywords.Add("H"); kw.Keywords.Add("V"); kw.AllowNone = true;
            var kwRes = ed.GetKeywords(kw);
            bool horizontal = kwRes.Status != PromptStatus.OK || kwRes.StringResult != "V";

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n배치할 텍스트 선택(순서대로): " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK || selRes.Value.Count < 2) return;

            using var tr = db.TransactionManager.StartTransaction();
            var items = selRes.Value.Cast<SelectedObject>()
                .Select(so => tr.GetObject(so.ObjectId, OpenMode.ForWrite))
                .ToList();

            Point3d GetPos(DBObject obj) => obj is DBText dt ? dt.Position
                                         : obj is MText mt ? mt.Location : Point3d.Origin;
            void SetPos(DBObject obj, Point3d p)
            {
                if (obj is DBText dt) dt.Position = p;
                else if (obj is MText mt) mt.Location = p;
            }

            var first = GetPos(items[0]);
            var last = GetPos(items[items.Count - 1]);
            int n = items.Count;
            for (int i = 1; i < n - 1; i++)
            {
                double t = (double)i / (n - 1);
                var cur = GetPos(items[i]);
                SetPos(items[i], horizontal
                    ? new Point3d(first.X + (last.X - first.X) * t, cur.Y, cur.Z)
                    : new Point3d(cur.X, first.Y + (last.Y - first.Y) * t, cur.Z));
            }
            tr.Commit();
            ed.WriteMessage($"\n{n}개 텍스트를 균등 배치했습니다.");
        }

        // ══════════════════════════════════════════════
        //  문자 II — 고급 편집
        // ══════════════════════════════════════════════

        /// <summary>문자 스타일 병합 — 같은 이름이 아닌 중복 스타일을 하나로 합치기 (TSMRG)</summary>
        [CommandMethod("DP_TSMRG")]
        public void TextStyleMerge()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var fromRes = ed.GetString(new PromptStringOptions("\n합칠 스타일 이름: "));
            if (fromRes.Status != PromptStatus.OK) return;
            var toRes = ed.GetString(new PromptStringOptions("\n목적지 스타일 이름: "));
            if (toRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!tst.Has(fromRes.StringResult) || !tst.Has(toRes.StringResult))
            { ed.WriteMessage("\n스타일 없음."); return; }

            var toId = tst[toRes.StringResult];
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) { tr.Commit(); return; }

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt && dt.TextStyleId == tst[fromRes.StringResult])
                { dt.TextStyleId = toId; count++; }
                else if (obj is MText mt && mt.TextStyleId == tst[fromRes.StringResult])
                { mt.TextStyleId = toId; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 문자 스타일을 병합했습니다.");
        }

        /// <summary>문자 줄 간격 변경 (TLEADING)</summary>
        [CommandMethod("DP_TLEADING")]
        public void ChangeTextLineSpacing()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var lsRes = ed.GetDouble(new PromptDoubleOptions("\n줄 간격 계수(예: 1.5): ")
            { DefaultValue = 1.5, AllowNegative = false });
            if (lsRes.Status != PromptStatus.OK) return;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\nMText 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is MText mt)
                {
                    mt.LineSpacingStyle = LineSpacingStyle.AtLeast;
                    mt.LineSpacingFactor = lsRes.Value;
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 MText 줄 간격을 {lsRes.Value}로 변경했습니다.");
        }

        /// <summary>문자 회전 각도 변경 (TROT)</summary>
        [CommandMethod("DP_TROT")]
        public void ChangeTextRotation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var angRes = ed.GetDouble(new PromptDoubleOptions("\n회전 각도(도): ") { DefaultValue = 0 });
            if (angRes.Status != PromptStatus.OK) return;
            double rad = angRes.Value * Math.PI / 180.0;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.Rotation = rad; count++; }
                else if (obj is MText mt) { mt.Rotation = rad; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 회전각 변경 완료.");
        }

        /// <summary>문자 수직 정렬 변경 (TJUST)</summary>
        [CommandMethod("DP_TJUST")]
        public void ChangeTextJustification()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var kwOpt = new PromptKeywordOptions("\n정렬 [왼쪽(L)/중앙(C)/오른쪽(R)] <L>: ");
            kwOpt.Keywords.Add("L"); kwOpt.Keywords.Add("C"); kwOpt.Keywords.Add("R");
            kwOpt.AllowNone = true;
            var kwRes = ed.GetKeywords(kwOpt);
            var mode = kwRes.Status == PromptStatus.OK ? kwRes.StringResult : "L";

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            var justify = mode switch
            {
                "C" => TextHorizontalMode.TextCenter,
                "R" => TextHorizontalMode.TextRight,
                _ => TextHorizontalMode.TextLeft
            };

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is DBText dt)
                { dt.HorizontalMode = justify; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 정렬 변경 완료.");
        }

        /// <summary>문자 폭 비율 변경 (TWSCALE)</summary>
        [CommandMethod("DP_TWSCALE")]
        public void ChangeTextWidthFactor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var wRes = ed.GetDouble(new PromptDoubleOptions("\n폭 비율(예: 0.7): ")
            { DefaultValue = 0.7, AllowNegative = false, AllowZero = false });
            if (wRes.Status != PromptStatus.OK) return;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\nText 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is DBText dt)
                { dt.WidthFactor = wRes.Value; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 폭 비율 변경 완료.");
        }

        /// <summary>문자 박스 그리기 — 텍스트 주위에 사각형 그리기 (TBOX)</summary>
        [CommandMethod("DP_TBOX")]
        public void TextBox()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var marginRes = ed.GetDouble(new PromptDoubleOptions("\n여백(0=경계선): ")
            { DefaultValue = 0.5, AllowNegative = false, AllowNone = true });
            double margin = marginRes.Status == PromptStatus.OK ? marginRes.Value : 0.5;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                var ext = ent.GeometricExtents;
                var min = ext.MinPoint;
                var max = ext.MaxPoint;
                var poly = new Polyline(4);
                poly.AddVertexAt(0, new Point2d(min.X - margin, min.Y - margin), 0, 0, 0);
                poly.AddVertexAt(1, new Point2d(max.X + margin, min.Y - margin), 0, 0, 0);
                poly.AddVertexAt(2, new Point2d(max.X + margin, max.Y + margin), 0, 0, 0);
                poly.AddVertexAt(3, new Point2d(min.X - margin, max.Y + margin), 0, 0, 0);
                poly.Closed = true;
                poly.Layer = ent.Layer;
                ms.AppendEntity(poly);
                tr.AddNewlyCreatedDBObject(poly, true);
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 박스를 그렸습니다.");
        }

        /// <summary>문자 번호 순번 매기기 (TNUM)</summary>
        [CommandMethod("DP_TNUM")]
        public void TextNumbering()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var startRes = ed.GetInteger(new PromptIntegerOptions("\n시작 번호: ")
            { DefaultValue = 1, LowerLimit = 0 });
            if (startRes.Status != PromptStatus.OK) return;

            var prefixRes = ed.GetString(new PromptStringOptions("\n접두어(없으면 Enter): ")
            { AllowSpaces = false });

            var selRes = ed.GetSelection(new PromptSelectionOptions
            { MessageForAdding = "\n텍스트 선택 (순서 중요): " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int num = startRes.Value;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                var newText = $"{prefixRes.StringResult}{num}";
                if (obj is DBText dt) dt.TextString = newText;
                else if (obj is MText mt) mt.Contents = newText;
                num++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{selRes.Value.Count}개 텍스트에 번호를 매겼습니다.");
        }

        /// <summary>문자 접두/접미 추가 (TPREFIX)</summary>
        [CommandMethod("DP_TPREFIX")]
        public void TextAddPrefixSuffix()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var preRes = ed.GetString(new PromptStringOptions("\n접두어(없으면 Enter): ")
            { AllowSpaces = true });
            var sufRes = ed.GetString(new PromptStringOptions("\n접미어(없으면 Enter): ")
            { AllowSpaces = true });

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            string pre = preRes.Status == PromptStatus.OK ? preRes.StringResult : "";
            string suf = sufRes.Status == PromptStatus.OK ? sufRes.StringResult : "";

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.TextString = pre + dt.TextString + suf; count++; }
                else if (obj is MText mt) { mt.Contents = pre + mt.Text + suf; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트에 접두/접미어를 추가했습니다.");
        }

        /// <summary>문자 대소문자 변환 (TCASE)</summary>
        [CommandMethod("DP_TCASE")]
        public void ChangeTextCase()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var kwOpt = new PromptKeywordOptions("\n변환 [대문자(U)/소문자(L)/첫글자대문자(T)] <U>: ");
            kwOpt.Keywords.Add("U"); kwOpt.Keywords.Add("L"); kwOpt.Keywords.Add("T");
            kwOpt.AllowNone = true;
            var kwRes = ed.GetKeywords(kwOpt);
            var mode = kwRes.Status == PromptStatus.OK ? kwRes.StringResult : "U";

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                string Convert(string s) => mode switch
                {
                    "U" => s.ToUpper(),
                    "L" => s.ToLower(),
                    "T" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower()),
                    _ => s
                };
                if (obj is DBText dt) { dt.TextString = Convert(dt.TextString); count++; }
                else if (obj is MText mt) { mt.Contents = Convert(mt.Text); count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 대소문자 변환 완료.");
        }

        /// <summary>문자 위치 이동 — 기준 점 기준으로 일괄 이동 (TMOVE)</summary>
        [CommandMethod("DP_TMOVE")]
        public void MoveTextToPoint()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n이동할 텍스트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") }));
            if (selRes.Status != PromptStatus.OK) return;

            var fromRes = ed.GetPoint(new PromptPointOptions("\n기준점: "));
            if (fromRes.Status != PromptStatus.OK) return;
            var toRes = ed.GetPoint(new PromptPointOptions("\n이동 목적지: "));
            if (toRes.Status != PromptStatus.OK) return;

            var delta = toRes.Value - fromRes.Value;
            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.Position += delta; count++; }
                else if (obj is MText mt) { mt.Location += delta; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트를 이동했습니다.");
        }

        // ══════════════════════════════════════════════
        //  이전 명령어 유지 (DP_TEXT_*)
        // ══════════════════════════════════════════════

        [CommandMethod("DP_TEXT_HEIGHT")]
        public void ChangeTextHeight()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;
            var heightRes = ed.GetDouble(new PromptDoubleOptions("\n새 텍스트 높이: ")
            { AllowNegative = false, AllowZero = false });
            if (heightRes.Status != PromptStatus.OK) return;
            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.Height = heightRes.Value; count++; }
                else if (obj is MText mt) { mt.TextHeight = heightRes.Value; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 높이 변경 완료.");
        }

        [CommandMethod("DP_TEXT_STYLE")]
        public void ChangeTextStyle()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var styleRes = ed.GetString(new PromptStringOptions("\n텍스트 스타일 이름: "));
            if (styleRes.Status != PromptStatus.OK) return;
            using var checkTr = db.TransactionManager.StartTransaction();
            var tt = (TextStyleTable)checkTr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!tt.Has(styleRes.StringResult)) { ed.WriteMessage("\n스타일 없음."); return; }
            var styleId = tt[styleRes.StringResult]; checkTr.Commit();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;
            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.TextStyleId = styleId; count++; }
                else if (obj is MText mt) { mt.TextStyleId = styleId; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 스타일 변경 완료.");
        }

        [CommandMethod("DP_TEXT_LAYER")]
        public void MoveTextToLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var layerRes = ed.GetString(new PromptStringOptions("\n이동할 레이어 이름: "));
            if (layerRes.Status != PromptStatus.OK) return;
            using var checkTr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)checkTr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }
            checkTr.Commit();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;
            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                ent.Layer = layerRes.StringResult; count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 레이어 이동 완료.");
        }

        [CommandMethod("DP_DIM_STYLE")]
        public void ChangeDimStyle()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var styleRes = ed.GetString(new PromptStringOptions("\n치수 스타일 이름: "));
            if (styleRes.Status != PromptStatus.OK) return;
            using var checkTr = db.TransactionManager.StartTransaction();
            var dst = (DimStyleTable)checkTr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!dst.Has(styleRes.StringResult)) { ed.WriteMessage("\n스타일 없음."); return; }
            var styleId = dst[styleRes.StringResult]; checkTr.Commit();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n치수 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;
            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Dimension dim)
                { dim.DimStyleId = styleId; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 치수 스타일 변경 완료.");
        }

        [CommandMethod("DP_DIM_SCALE")]
        public void ChangeDimScale()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var scaleRes = ed.GetDouble(new PromptDoubleOptions("\n치수 전체 축척: ")
            { DefaultValue = 1.0, AllowNegative = false, AllowZero = false });
            if (scaleRes.Status != PromptStatus.OK) return;
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n치수 선택: " }, filter);
            if (selRes.Status == PromptStatus.Error) selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) return;
            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Dimension dim)
                {
                    dim.Dimscale = scaleRes.Value;
                    dim.RecomputeDimensionBlock(true);
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 치수 축척 변경 완료.");
        }

        /// <summary>치수선 레이어 이동 (DMLAYER)</summary>
        [CommandMethod("DP_DIM_LAYER")]
        public void MoveDimToLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var layerRes = ed.GetString(new PromptStringOptions("\n치수를 이동할 레이어 이름: "));
            if (layerRes.Status != PromptStatus.OK) return;

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n치수 선택: " }, filter);
            if (selRes.Status == PromptStatus.Error) selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                ent.Layer = layerRes.StringResult;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 치수를 '{layerRes.StringResult}' 레이어로 이동했습니다.");
        }

        /// <summary>치수 스타일 병합 (DIMSMRG)</summary>
        [CommandMethod("DP_DIM_MERGE")]
        public void DimStyleMerge()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var fromRes = ed.GetString(new PromptStringOptions("\n병합할 치수 스타일: "));
            if (fromRes.Status != PromptStatus.OK) return;
            var toRes = ed.GetString(new PromptStringOptions("\n목적지 치수 스타일: "));
            if (toRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!dst.Has(fromRes.StringResult) || !dst.Has(toRes.StringResult))
            { ed.WriteMessage("\n스타일 없음."); return; }

            var fromId = dst[fromRes.StringResult];
            var toId = dst[toRes.StringResult];

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });
            var selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) { tr.Commit(); return; }

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Dimension dim
                    && dim.DimStyleId == fromId)
                { dim.DimStyleId = toId; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 치수 스타일을 병합했습니다.");
        }
    }
}
