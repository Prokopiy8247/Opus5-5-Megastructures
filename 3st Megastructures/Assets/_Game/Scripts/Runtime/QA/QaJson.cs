// QaJson: a tiny JSON builder for the QA reports (MASTER_PROMPT 11.2, 11.8). Reports are written once,
// at the end of a run, never per frame. Invariant-culture numbers, NaN / Inf written as null, optional
// inline containers for short arrays. Pure C# (no UnityEngine), so EditMode tests and tools can use it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Megastructure.QA
{
    /// <summary>Minimal JSON writer: objects, arrays, invariant-culture numbers, NaN/Inf as null.</summary>
    public sealed class QaJson
    {
        readonly StringBuilder m_sb = new StringBuilder(1 << 14);
        // per open container: true while no element has been written yet
        readonly List<bool> m_first = new List<bool>(16);
        readonly List<bool> m_inline = new List<bool>(16);
        bool m_afterKey;
        int m_inlineDepth;

        public QaJson BeginObject(bool inline = false) { Separator(); m_sb.Append('{'); Push(inline); return this; }
        public QaJson EndObject() { Pop('}'); return this; }
        public QaJson BeginArray(bool inline = false) { Separator(); m_sb.Append('['); Push(inline); return this; }
        public QaJson EndArray() { Pop(']'); return this; }

        public QaJson Key(string key)
        {
            Separator();
            AppendString(key);
            m_sb.Append(m_inlineDepth > 0 ? ":" : ": ");
            m_afterKey = true;
            return this;
        }

        public QaJson Value(string v) { Separator(); if (v == null) m_sb.Append("null"); else AppendString(v); return this; }
        public QaJson Value(bool v) { Separator(); m_sb.Append(v ? "true" : "false"); return this; }
        public QaJson Value(int v) { Separator(); m_sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public QaJson Value(long v) { Separator(); m_sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public QaJson Value(double v) { Separator(); AppendNumber(v); return this; }

        public QaJson Prop(string key, string v) => Key(key).Value(v);
        public QaJson Prop(string key, bool v) => Key(key).Value(v);
        public QaJson Prop(string key, int v) => Key(key).Value(v);
        public QaJson Prop(string key, long v) => Key(key).Value(v);
        public QaJson Prop(string key, double v) => Key(key).Value(v);

        public QaJson Prop(string key, double x, double y, double z)
        {
            Key(key).BeginArray(true).Value(x).Value(y).Value(z).EndArray();
            return this;
        }

        public QaJson Prop(string key, IList<double> values)
        {
            Key(key).BeginArray(true);
            if (values != null) for (int i = 0; i < values.Count; i++) Value(values[i]);
            return EndArray();
        }

        public QaJson Prop(string key, IList<string> values)
        {
            Key(key).BeginArray(values == null || values.Count < 8);
            if (values != null) for (int i = 0; i < values.Count; i++) Value(values[i]);
            return EndArray();
        }

        public override string ToString() => m_sb.ToString();

        // ------------------------------------------------------------------ internals
        void Push(bool inline)
        {
            bool il = inline || m_inlineDepth > 0;
            m_first.Add(true);
            m_inline.Add(il);
            if (il) m_inlineDepth++;
        }

        void Pop(char close)
        {
            int last = m_first.Count - 1;
            if (last < 0) throw new InvalidOperationException("QaJson: unbalanced close");
            bool wasEmpty = m_first[last];
            bool il = m_inline[last];
            m_first.RemoveAt(last);
            m_inline.RemoveAt(last);
            if (il) m_inlineDepth--;
            else if (!wasEmpty) NewLine(m_first.Count);
            m_sb.Append(close);
        }

        void Separator()
        {
            if (m_afterKey) { m_afterKey = false; return; }
            int last = m_first.Count - 1;
            if (last < 0) return;
            if (m_first[last]) m_first[last] = false;
            else m_sb.Append(m_inline[last] ? ", " : ",");
            if (!m_inline[last]) NewLine(m_first.Count);
        }

        void NewLine(int depth)
        {
            m_sb.Append('\n');
            for (int i = 0; i < depth; i++) m_sb.Append("  ");
        }

        void AppendNumber(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) { m_sb.Append("null"); return; }
            double a = Math.Abs(v);
            string s = (a != 0 && (a < 1e-5 || a >= 1e15))
                ? v.ToString("0.######E+0", CultureInfo.InvariantCulture)
                : v.ToString("0.######", CultureInfo.InvariantCulture);
            m_sb.Append(s);
        }

        void AppendString(string s)
        {
            m_sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': m_sb.Append("\\\""); break;
                    case '\\': m_sb.Append("\\\\"); break;
                    case '\n': m_sb.Append("\\n"); break;
                    case '\r': m_sb.Append("\\r"); break;
                    case '\t': m_sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) m_sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else m_sb.Append(c);
                        break;
                }
            }
            m_sb.Append('"');
        }

        /// <summary>Formats a double the way the JSON writer does (tests, CSV).</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
