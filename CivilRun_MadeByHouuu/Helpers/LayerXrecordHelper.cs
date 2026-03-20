using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilRun_MadeByHouuu.Helpers
{
    /// <summary>
    /// Xrecord 기반 레이어 OFF 상태 영구/임시 저장소.
    /// LOF/LOFF/LON/LONN/LOL 계열 명령에서 공유합니다.
    /// </summary>
    internal static class LayerXrecordHelper
    {
        internal const string DictName     = "DreamLikeTools";
        internal const string XrecLoffName = "LOFF_LAYERS";   // 영구 OFF 목록
        internal const string XrecTempName = "TEMP_OFF_LAYERS"; // 임시 OFF 목록

        internal static HashSet<string> LoadSet(Database db, Transaction tr, string xrecName)
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(DictName)) return set;
            var sub = (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);
            if (!sub.Contains(xrecName)) return set;
            var xr = (Xrecord)tr.GetObject(sub.GetAt(xrecName), OpenMode.ForRead);
            if (xr.Data == null) return set;
            foreach (TypedValue tv in xr.Data)
                if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string s && !string.IsNullOrWhiteSpace(s))
                    set.Add(s);
            return set;
        }

        internal static void SaveSet(Database db, Transaction tr, string xrecName, HashSet<string> set)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary sub;
            if (!nod.Contains(DictName))
            {
                nod.UpgradeOpen();
                sub = new DBDictionary();
                nod.SetAt(DictName, sub);
                tr.AddNewlyCreatedDBObject(sub, true);
            }
            else sub = (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForWrite);

            Xrecord xr;
            if (!sub.Contains(xrecName))
            {
                xr = new Xrecord();
                sub.SetAt(xrecName, xr);
                tr.AddNewlyCreatedDBObject(xr, true);
            }
            else xr = (Xrecord)tr.GetObject(sub.GetAt(xrecName), OpenMode.ForWrite);

            var tvs = new List<TypedValue>();
            foreach (var name in set) tvs.Add(new TypedValue((int)DxfCode.Text, name));
            xr.Data = new ResultBuffer(tvs.ToArray());
        }

        internal static void AddToSet(Database db, Transaction tr, string xrecName, IEnumerable<string> names)
        {
            var set = LoadSet(db, tr, xrecName);
            foreach (var n in names) set.Add(n);
            SaveSet(db, tr, xrecName, set);
        }

        internal static void ClearSet(Database db, Transaction tr, string xrecName) =>
            SaveSet(db, tr, xrecName, new HashSet<string>(System.StringComparer.OrdinalIgnoreCase));
    }
}
