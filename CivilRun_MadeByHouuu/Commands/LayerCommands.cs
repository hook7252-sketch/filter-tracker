using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    /// <summary>
    /// 레이어 관리 명령어 (약 50여 개)
    /// 카테고리: 레이어I, 레이어II
    /// </summary>
    public class LayerCommands
    {
        // ══════════════════════════════════════════════
        //  레이어 I — 기본 ON/OFF/잠금/동결
        // ══════════════════════════════════════════════

        /// <summary>선택 객체의 레이어 끄기 (LAYOFF)</summary>
        [CommandMethod("DP_LOF", CommandFlags.UsePickSet)]
        public void LayerOff()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n끌 레이어의 객체 선택: " });
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var names = new HashSet<string>();
            foreach (SelectedObject so in selRes.Value)
                names.Add(((Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead)).Layer);

            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var name in names)
            {
                if (!lt.Has(name)) continue;
                var layer = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForWrite);
                layer.IsOff = true;
            }
            tr.Commit();
            ed.WriteMessage($"\n{names.Count}개 레이어를 껐습니다.");
        }

        /// <summary>모든 레이어 켜기</summary>
        [CommandMethod("DP_LON")]
        public void LayerOnAll()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            int count = 0;
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (l.IsOff) { l.IsOff = false; count++; }
            }
            tr.Commit();
            doc.Editor.WriteMessage($"\n{count}개 레이어를 켰습니다.");
        }

        /// <summary>선택 객체 레이어만 켜고 나머지 끄기 (레이어 분리)</summary>
        [CommandMethod("DP_LISO")]
        public void LayerIsolate()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n유지할 레이어의 객체 선택: " });
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var keepNames = new HashSet<string>();
            foreach (SelectedObject so in selRes.Value)
                keepNames.Add(((Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead)).Layer);

            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            int count = 0;
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (!keepNames.Contains(l.Name) && l.Name != "0")
                { l.IsOff = true; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{keepNames.Count}개 레이어를 유지하고 {count}개 레이어를 끄켰습니다.");
        }

        /// <summary>레이어 분리 해제 (모두 복원)</summary>
        [CommandMethod("DP_LUISO")]
        public void LayerUnisolate() => new LayerCommands().LayerOnAll();

        /// <summary>선택 객체의 레이어 잠금</summary>
        [CommandMethod("DP_LLK", CommandFlags.UsePickSet)]
        public void LayerLock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n잠글 레이어의 객체 선택: " });
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var names = new HashSet<string>();
            foreach (SelectedObject so in selRes.Value)
                names.Add(((Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead)).Layer);

            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var name in names)
            {
                if (!lt.Has(name)) continue;
                var layer = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForWrite);
                layer.IsLocked = true;
            }
            tr.Commit();
            ed.WriteMessage($"\n{names.Count}개 레이어를 잠갔습니다.");
        }

        /// <summary>모든 레이어 잠금 해제</summary>
        [CommandMethod("DP_LULK")]
        public void LayerUnlockAll()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            int count = 0;
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (l.IsLocked) { l.IsLocked = false; count++; }
            }
            tr.Commit();
            doc.Editor.WriteMessage($"\n{count}개 레이어 잠금을 해제했습니다.");
        }

        /// <summary>선택 객체의 레이어를 현재 레이어로 변경</summary>
        [CommandMethod("DP_LCC", CommandFlags.UsePickSet)]
        public void ChangeLayerToCurrent()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n객체 선택: " });
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var curLayerName = ((LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead)).Name;
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                ent.Layer = curLayerName;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 객체를 '{curLayerName}' 레이어로 변경했습니다.");
        }

        /// <summary>선택한 객체의 레이어를 현재 레이어로 설정</summary>
        [CommandMethod("DP_LCUR")]
        public void SetCurrentLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n현재 레이어로 설정할 객체 선택: " });
            if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ent = (Entity)tr.GetObject(selRes.Value[0].ObjectId, OpenMode.ForRead);
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            db.Clayer = lt[ent.Layer];
            tr.Commit();
            ed.WriteMessage($"\n현재 레이어를 '{ent.Layer}'로 설정했습니다.");
        }

        // ══════════════════════════════════════════════
        //  레이어 II — 고급 관리
        // ══════════════════════════════════════════════

        /// <summary>선택 객체를 다른 레이어로 이동 (MEO)</summary>
        [CommandMethod("DP_MEO")]
        public void MoveEntityToOtherLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n이동할 객체 선택: " });
            if (selRes.Status != PromptStatus.OK) return;

            var layerRes = ed.GetString(new PromptStringOptions("\n이동할 레이어 이름: ") { AllowSpaces = false });
            if (layerRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerRes.StringResult))
            {
                // 레이어 없으면 생성
                lt.UpgradeOpen();
                var newLayer = new LayerTableRecord { Name = layerRes.StringResult };
                lt.Add(newLayer);
                tr.AddNewlyCreatedDBObject(newLayer, true);
            }
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                ent.Layer = layerRes.StringResult;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 객체를 '{layerRes.StringResult}' 레이어로 이동했습니다.");
        }

        /// <summary>레이어 맞춤 — 0 레이어 객체를 기준으로 다른 객체 레이어 통일 (LMA)</summary>
        [CommandMethod("DP_LMA")]
        public void LayerMatch()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var srcRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n기준 레이어 객체 선택 (1개): " });
            if (srcRes.Status != PromptStatus.OK || srcRes.Value.Count == 0) return;

            using var srcTr = db.TransactionManager.StartTransaction();
            var srcLayer = ((Entity)srcTr.GetObject(srcRes.Value[0].ObjectId, OpenMode.ForRead)).Layer;
            srcTr.Commit();

            var dstRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n변경할 객체 선택: " });
            if (dstRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in dstRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                ent.Layer = srcLayer;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 객체를 '{srcLayer}' 레이어로 맞췄습니다.");
        }

        /// <summary>레이어 병합 — 여러 레이어를 하나로 합치기 (LME)</summary>
        [CommandMethod("DP_LME")]
        public void LayerMerge()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var fromRes = ed.GetString(new PromptStringOptions("\n합칠 레이어 이름(쉼표 구분): ") { AllowSpaces = true });
            if (fromRes.Status != PromptStatus.OK) return;

            var toRes = ed.GetString(new PromptStringOptions("\n합칠 목적지 레이어 이름: "));
            if (toRes.Status != PromptStatus.OK) return;

            var fromNames = fromRes.StringResult.Split(',').Select(n => n.Trim()).ToList();
            var toName = toRes.StringResult.Trim();

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            // 목적지 레이어 없으면 생성
            if (!lt.Has(toName))
            {
                lt.UpgradeOpen();
                var newLayer = new LayerTableRecord { Name = toName };
                lt.Add(newLayer);
                tr.AddNewlyCreatedDBObject(newLayer, true);
            }

            // 모델/페이퍼 스페이스 전체 순회
            int count = 0;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId bId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(bId, OpenMode.ForRead);
                foreach (ObjectId eId in btr)
                {
                    if (tr.GetObject(eId, OpenMode.ForRead) is Entity ent && fromNames.Contains(ent.Layer))
                    {
                        ent.UpgradeOpen();
                        ent.Layer = toName;
                        count++;
                    }
                }
            }

            // 원본 레이어 삭제
            foreach (var name in fromNames)
            {
                if (!lt.Has(name)) continue;
                var layer = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForWrite);
                try { layer.Erase(); } catch { /* 참조 중이면 스킵 */ }
            }

            tr.Commit();
            ed.WriteMessage($"\n{fromNames.Count}개 레이어를 '{toName}'으로 병합 ({count}개 객체)했습니다.");
        }

        /// <summary>색상별 레이어 정리 — 객체 색상을 레이어 색상으로 통일 (LC)</summary>
        [CommandMethod("DP_LC")]
        public void LayerColorOrganize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n정리할 객체 선택 (Enter=전체): " });
            if (selRes.Status == PromptStatus.Error) selRes = ed.SelectAll();
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                // 객체 색상을 BYLAYER로 통일
                ent.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 객체 색상을 레이어 색상(BYLAYER)으로 통일했습니다.");
        }

        /// <summary>레이어 이름 변경</summary>
        [CommandMethod("DP_LRENAME")]
        public void RenameLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var oldRes = ed.GetString(new PromptStringOptions("\n현재 레이어 이름: "));
            if (oldRes.Status != PromptStatus.OK) return;

            var newRes = ed.GetString(new PromptStringOptions("\n새 레이어 이름: "));
            if (newRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(oldRes.StringResult))
            { ed.WriteMessage($"\n'{oldRes.StringResult}' 레이어를 찾을 수 없습니다."); return; }

            var layer = (LayerTableRecord)tr.GetObject(lt[oldRes.StringResult], OpenMode.ForWrite);
            layer.Name = newRes.StringResult;
            tr.Commit();
            ed.WriteMessage($"\n'{oldRes.StringResult}' → '{newRes.StringResult}' 이름 변경 완료.");
        }

        /// <summary>레이어 복사 — 레이어 속성(색상/선종류)을 다른 레이어에 복사</summary>
        [CommandMethod("DP_LCOPY")]
        public void CopyLayerProperties()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var srcRes = ed.GetString(new PromptStringOptions("\n복사할 레이어 이름: "));
            if (srcRes.Status != PromptStatus.OK) return;
            var dstRes = ed.GetString(new PromptStringOptions("\n붙여넣을 레이어 이름: "));
            if (dstRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(srcRes.StringResult) || !lt.Has(dstRes.StringResult))
            { ed.WriteMessage("\n레이어를 찾을 수 없습니다."); return; }

            var src = (LayerTableRecord)tr.GetObject(lt[srcRes.StringResult], OpenMode.ForRead);
            var dst = (LayerTableRecord)tr.GetObject(lt[dstRes.StringResult], OpenMode.ForWrite);
            dst.Color = src.Color;
            dst.LinetypeObjectId = src.LinetypeObjectId;
            dst.LineWeight = src.LineWeight;
            tr.Commit();
            ed.WriteMessage($"\n레이어 속성을 '{srcRes.StringResult}' → '{dstRes.StringResult}'으로 복사했습니다.");
        }

        /// <summary>레이어 선종류 일괄 변경</summary>
        [CommandMethod("DP_LLINETYPE")]
        public void ChangeLayerLinetype()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n레이어 이름: "));
            if (nameRes.Status != PromptStatus.OK) return;
            var ltRes = ed.GetString(new PromptStringOptions("\n선종류 이름: "));
            if (ltRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var layerTbl = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!layerTbl.Has(nameRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }

            var ltTbl = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (!ltTbl.Has(ltRes.StringResult)) { ed.WriteMessage($"\n선종류 '{ltRes.StringResult}' 없음."); return; }

            var layer = (LayerTableRecord)tr.GetObject(layerTbl[nameRes.StringResult], OpenMode.ForWrite);
            layer.LinetypeObjectId = ltTbl[ltRes.StringResult];
            tr.Commit();
            ed.WriteMessage($"\n레이어 '{nameRes.StringResult}' 선종류를 '{ltRes.StringResult}'으로 변경했습니다.");
        }

        /// <summary>레이어 선가중치 변경</summary>
        [CommandMethod("DP_LLWEIGHT")]
        public void ChangeLayerLineWeight()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n레이어 이름: "));
            if (nameRes.Status != PromptStatus.OK) return;

            var lwOpt = new PromptIntegerOptions("\n선가중치(예: 25=0.25mm, 50=0.5mm, 100=1mm): ")
            { LowerLimit = 0, UpperLimit = 211 };
            var lwRes = ed.GetInteger(lwOpt);
            if (lwRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(nameRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }

            var layer = (LayerTableRecord)tr.GetObject(lt[nameRes.StringResult], OpenMode.ForWrite);
            layer.LineWeight = (LineWeight)lwRes.Value;
            tr.Commit();
            ed.WriteMessage($"\n레이어 선가중치를 변경했습니다.");
        }

        /// <summary>레이어 투명도 변경</summary>
        [CommandMethod("DP_LTRANS")]
        public void ChangeLayerTransparency()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n레이어 이름: "));
            if (nameRes.Status != PromptStatus.OK) return;
            var transRes = ed.GetInteger(new PromptIntegerOptions("\n투명도(0-90): ")
            { LowerLimit = 0, UpperLimit = 90 });
            if (transRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(nameRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }

            var layer = (LayerTableRecord)tr.GetObject(lt[nameRes.StringResult], OpenMode.ForWrite);
            layer.Transparency = new Autodesk.AutoCAD.Colors.Transparency(
                (byte)(255 * transRes.Value / 100));
            tr.Commit();
            ed.WriteMessage($"\n레이어 투명도를 {transRes.Value}%로 변경했습니다.");
        }

        /// <summary>객체 색상별로 레이어 자동 분류 생성</summary>
        [CommandMethod("DP_LBYCOLOR")]
        public void SortLayerByColor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.SelectAll();
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            int moved = 0;

            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForRead) is not Entity ent) continue;
                if (ent.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByLayer) continue;

                var colorIdx = ent.Color.ColorIndex;
                var layerName = $"COLOR_{colorIdx}";

                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    var newLayer = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIdx)
                    };
                    lt.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }

                ent.UpgradeOpen();
                ent.Layer = layerName;
                ent.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                moved++;
            }

            tr.Commit();
            ed.WriteMessage($"\n{moved}개 객체를 색상별 레이어로 분류했습니다.");
        }

        /// <summary>레이어 상태 저장</summary>
        [CommandMethod("DP_LSAVE")]
        public void SaveLayerState()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var nameRes = ed.GetString(new PromptStringOptions("\n저장할 레이어 상태 이름: "));
            if (nameRes.Status != PromptStatus.OK) return;

            var lsm = doc.Database.LayerStateManager;
            if (lsm.HasLayerState(nameRes.StringResult))
                lsm.DeleteLayerState(nameRes.StringResult);
            lsm.SaveLayerState(nameRes.StringResult,
                LayerStateMasks.Color | LayerStateMasks.Frozen |
                LayerStateMasks.Locked | LayerStateMasks.Plot, ObjectId.Null);
            ed.WriteMessage($"\n레이어 상태 '{nameRes.StringResult}'을 저장했습니다.");
        }

        /// <summary>레이어 상태 복원</summary>
        [CommandMethod("DP_LRESTORE")]
        public void RestoreLayerState()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var nameRes = ed.GetString(new PromptStringOptions("\n복원할 레이어 상태 이름: "));
            if (nameRes.Status != PromptStatus.OK) return;

            var lsm = doc.Database.LayerStateManager;
            if (!lsm.HasLayerState(nameRes.StringResult))
            { ed.WriteMessage($"\n'{nameRes.StringResult}' 레이어 상태가 없습니다."); return; }

            lsm.RestoreLayerState(nameRes.StringResult, ObjectId.Null, 0,
                LayerStateMasks.Color | LayerStateMasks.Frozen |
                LayerStateMasks.Locked | LayerStateMasks.Plot);
            ed.WriteMessage($"\n레이어 상태 '{nameRes.StringResult}'을 복원했습니다.");
        }

        /// <summary>모든 레이어 동결 (AF)</summary>
        [CommandMethod("DP_AF")]
        public void FreezeAllLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var currentId = db.Clayer;
            int count = 0;
            foreach (ObjectId id in lt)
            {
                if (id == currentId) continue;
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (!l.IsFrozen) { l.IsFrozen = true; count++; }
            }
            tr.Commit();
            doc.Editor.WriteMessage($"\n{count}개 레이어를 동결했습니다.");
        }

        /// <summary>현재 레이어 설정 (이름으로)</summary>
        [CommandMethod("DP_LSET")]
        public void SetLayerByName()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var res = ed.GetString(new PromptStringOptions("\n현재 레이어로 설정할 레이어 이름: "));
            if (res.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(res.StringResult)) { ed.WriteMessage($"\n'{res.StringResult}' 레이어 없음."); return; }

            db.Clayer = lt[res.StringResult];
            tr.Commit();
            ed.WriteMessage($"\n현재 레이어: '{res.StringResult}'");
        }

        /// <summary>잠긴 레이어 목록 출력</summary>
        [CommandMethod("DP_LLIST_LOCK")]
        public void ListLockedLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            ed.WriteMessage("\n── 잠긴 레이어 ──");
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (l.IsLocked) ed.WriteMessage($"\n  {l.Name}");
            }
            ed.WriteMessage("\n────────────────");
        }

        /// <summary>동결된 레이어 목록 출력</summary>
        [CommandMethod("DP_LLIST_FROZEN")]
        public void ListFrozenLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            ed.WriteMessage("\n── 동결된 레이어 ──");
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (l.IsFrozen) ed.WriteMessage($"\n  {l.Name}");
            }
            ed.WriteMessage("\n──────────────────");
        }

        // ══════════════════════════════════════════════
        // 아래는 LayerCommands.cs 원본 명령들 (유지)
        // ══════════════════════════════════════════════

        [CommandMethod("DP_LAYER_NEW")]
        public void CreateLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var opt = new PromptStringOptions("\n레이어 이름(쉼표로 구분): ") { AllowSpaces = true };
            var res = ed.GetString(opt);
            if (res.Status != PromptStatus.OK) return;
            var names = res.StringResult.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0);
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            int created = 0;
            foreach (var name in names)
            {
                if (lt.Has(name)) continue;
                lt.UpgradeOpen();
                var layer = new LayerTableRecord { Name = name };
                lt.Add(layer); tr.AddNewlyCreatedDBObject(layer, true); created++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{created}개의 레이어를 생성했습니다.");
        }

        [CommandMethod("DP_LAYER_COLOR")]
        public void ChangeLayerColor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var nameRes = ed.GetString(new PromptStringOptions("\n레이어 이름: ") { AllowSpaces = false });
            if (nameRes.Status != PromptStatus.OK) return;
            var colorRes = ed.GetInteger(new PromptIntegerOptions("\n색상 번호(1-255): ") { LowerLimit = 1, UpperLimit = 255 });
            if (colorRes.Status != PromptStatus.OK) return;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(nameRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }
            var layer = (LayerTableRecord)tr.GetObject(lt[nameRes.StringResult], OpenMode.ForWrite);
            layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorRes.Value);
            tr.Commit();
            ed.WriteMessage($"\n색상 변경 완료.");
        }

        [CommandMethod("DP_LAYER_FREEZE_OTHERS")]
        public void FreezeOtherLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var currentName = ((LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead)).Name;
            int count = 0;
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (layer.Name == currentName || layer.Name == "0") continue;
                if (!layer.IsFrozen) { layer.IsFrozen = true; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 레이어 동결 완료.");
        }

        [CommandMethod("DP_LAYER_THAW_ALL")]
        public void ThawAllLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            int count = 0;
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (layer.IsFrozen) { layer.IsFrozen = false; count++; }
            }
            tr.Commit();
            doc.Editor.WriteMessage($"\n{count}개 레이어 동결 해제 완료.");
        }

        [CommandMethod("DP_LAYER_LIST")]
        public void ListLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            ed.WriteMessage("\n──── 레이어 목록 ────");
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                var s = l.IsFrozen ? "동결" : l.IsOff ? "끔" : "켜짐";
                ed.WriteMessage($"\n  {l.Name,-20} 색:{l.Color.ColorIndex,3}  {s}");
            }
            ed.WriteMessage("\n─────────────────────");
        }

        [CommandMethod("DP_LAYER_DELETE_EMPTY")]
        public void DeleteEmptyLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var used = Helpers.LayerHelper.GetUsedLayerNames(db);
            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var toDelete = new List<ObjectId>();
            foreach (ObjectId id in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (l.Name is "0" or "Defpoints") continue;
                if (!used.Contains(l.Name)) toDelete.Add(id);
            }
            foreach (var id in toDelete)
                ((LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite)).Erase();
            tr.Commit();
            ed.WriteMessage($"\n{toDelete.Count}개 빈 레이어 삭제 완료.");
        }
    }
}
