using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Minimal indented JSON writer for the QA reports (report.json, autotest.json, ...). No reflection, no external
    /// dependency; NaN and infinities are written as null. QA-only (allocates).
    /// </summary>
    public sealed class QaJson
    {
        readonly StringBuilder _sb = new StringBuilder(1 << 14);
        readonly List<bool> _first = new List<bool>(16);
        bool _afterKey;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public override string ToString() => _sb.ToString();

        public void Save(string path)
        {
            QaPaths.EnsureParent(path);
            File.WriteAllText(path, _sb.ToString() + "\n", new UTF8Encoding(false));
        }

        // ---------------------------------------------------------------- structure
        public QaJson BeginObject() { Prefix(); _sb.Append('{'); _first.Add(true); return this; }
        public QaJson EndObject() { Close('}'); return this; }
        public QaJson BeginArray() { Prefix(); _sb.Append('['); _first.Add(true); return this; }
        public QaJson EndArray() { Close(']'); return this; }

        public QaJson Key(string k)
        {
            Prefix();
            Str(k);
            _sb.Append(": ");
            _afterKey = true;
            return this;
        }

        public QaJson Obj(string key) => Key(key).BeginObject();
        public QaJson Arr(string key) => Key(key).BeginArray();

        void Prefix()
        {
            if (_afterKey) { _afterKey = false; return; }
            int n = _first.Count;
            if (n == 0) return;
            if (!_first[n - 1]) _sb.Append(',');
            _first[n - 1] = false;
            _sb.Append('\n').Append(' ', 2 * n);
        }

        void Close(char c)
        {
            int n = _first.Count;
            if (n == 0) return;
            bool empty = _first[n - 1];
            _first.RemoveAt(n - 1);
            if (!empty) _sb.Append('\n').Append(' ', 2 * (n - 1));
            _sb.Append(c);
        }

        // ---------------------------------------------------------------- values
        public QaJson Value(string s) { Prefix(); if (s == null) _sb.Append("null"); else Str(s); return this; }
        public QaJson Value(bool b) { Prefix(); _sb.Append(b ? "true" : "false"); return this; }
        public QaJson Value(int v) { Prefix(); _sb.Append(v.ToString(Inv)); return this; }
        public QaJson Value(long v) { Prefix(); _sb.Append(v.ToString(Inv)); return this; }
        public QaJson Value(float v) { Prefix(); Num(v); return this; }
        public QaJson Value(double v) { Prefix(); Num(v); return this; }
        public QaJson Null() { Prefix(); _sb.Append("null"); return this; }

        /// <summary>[x, y, z] on one line.</summary>
        public QaJson Value(Vector3 v) { Prefix(); _sb.Append('['); Num(v.x); _sb.Append(", "); Num(v.y); _sb.Append(", "); Num(v.z); _sb.Append(']'); return this; }

        /// <summary>A short numeric array on one line.</summary>
        public QaJson Inline(params float[] v)
        {
            Prefix();
            _sb.Append('[');
            for (int i = 0; i < v.Length; i++) { if (i > 0) _sb.Append(", "); Num(v[i]); }
            _sb.Append(']');
            return this;
        }

        public QaJson Prop(string k, string v) => Key(k).Value(v);
        public QaJson Prop(string k, bool v) => Key(k).Value(v);
        public QaJson Prop(string k, int v) => Key(k).Value(v);
        public QaJson Prop(string k, long v) => Key(k).Value(v);
        public QaJson Prop(string k, float v) => Key(k).Value(v);
        public QaJson Prop(string k, double v) => Key(k).Value(v);
        public QaJson Prop(string k, Vector3 v) => Key(k).Value(v);
        public QaJson PropRect(string k, float x0, float y0, float x1, float y1) => Key(k).Inline(x0, y0, x1, y1);

        /// <summary>A pre-formatted raw JSON fragment as a value (trusted input only).</summary>
        public QaJson Raw(string json) { Prefix(); _sb.Append(json); return this; }

        void Num(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) { _sb.Append("null"); return; }
            _sb.Append(v.ToString("G7", Inv));
        }

        void Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) { _sb.Append("null"); return; }
            _sb.Append(v.ToString("0.0#########", Inv));
        }

        void Str(string s)
        {
            _sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        /// <summary>Invariant "F" formatting for log lines.</summary>
        public static string F(double v, int digits = 2) => v.ToString("F" + digits, Inv);
    }
}
