using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Yvsim.AppData
{
    /// <summary>按小步读取 JSON；对象、数组元素和每段最多 128 字符的文本都会交还控制权。</summary>
    internal sealed class AppDataJsonParser
    {
        internal sealed class Number
        {
            internal readonly string Text;
            internal Number(string text) { Text = text; }
        }

        private readonly string json;
        internal int Position { get; private set; }
        internal int NodeCount { get; private set; }
        internal object Value { get; private set; }

        internal AppDataJsonParser(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new InvalidDataException("JSON 内容不能为空。");
            this.json = json;
        }

        internal IEnumerable<int> Read()
        {
            foreach (int step in ReadValue(0)) yield return step;
            foreach (int step in WhiteSpace()) yield return step;
            if (Position != json.Length) throw Invalid("末尾存在多余内容");
        }

        private IEnumerable<int> ReadValue(int depth)
        {
            if (depth > 128) throw Invalid("嵌套层数超过 128");
            NodeCount++;
            yield return 0;
            foreach (int step in WhiteSpace()) yield return step;
            char token = Peek();
            if (token == '{')
            {
                Position++;
                var members = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (int step in WhiteSpace()) yield return step;
                if (Peek() != '}')
                {
                    while (true)
                    {
                        foreach (int step in WhiteSpace()) yield return step;
                        foreach (int step in ReadString()) yield return step;
                        string name = (string)Value;
                        foreach (int step in WhiteSpace()) yield return step;
                        Take(':');
                        foreach (int step in ReadValue(depth + 1)) yield return step;
                        if (members.ContainsKey(name)) throw Invalid("字段重复：" + name);
                        members.Add(name, Value);
                        foreach (int step in WhiteSpace()) yield return step;
                        if (Peek() != ',') break;
                        Position++;
                    }
                }
                Take('}');
                Value = members;
            }
            else if (token == '[')
            {
                Position++;
                var items = new List<object>();
                foreach (int step in WhiteSpace()) yield return step;
                if (Peek() != ']')
                {
                    while (true)
                    {
                        foreach (int step in ReadValue(depth + 1)) yield return step;
                        items.Add(Value);
                        foreach (int step in WhiteSpace()) yield return step;
                        if (Peek() != ',') break;
                        Position++;
                    }
                }
                Take(']');
                Value = items;
            }
            else if (token == '"')
            {
                foreach (int step in ReadString()) yield return step;
            }
            else if (token == 't') { Literal("true"); Value = true; }
            else if (token == 'f') { Literal("false"); Value = false; }
            else if (token == 'n') { Literal("null"); Value = null; }
            else
            {
                int start = Position;
                if (Peek() == '-') Position++;
                if (Peek() == '0') Position++;
                else
                {
                    if (!Digit(Peek())) throw Invalid("需要合法的 JSON 值");
                    while (Digit(Peek()))
                    {
                        Position++;
                        if (Position - start > 128) throw Invalid("数字过长");
                    }
                }
                if (Peek() == '.')
                {
                    Position++;
                    if (!Digit(Peek())) throw Invalid("小数点后缺少数字");
                    while (Digit(Peek()))
                    {
                        Position++;
                        if (Position - start > 128) throw Invalid("数字过长");
                    }
                }
                if (Peek() == 'e' || Peek() == 'E')
                {
                    Position++;
                    if (Peek() == '+' || Peek() == '-') Position++;
                    if (!Digit(Peek())) throw Invalid("指数缺少数字");
                    while (Digit(Peek()))
                    {
                        Position++;
                        if (Position - start > 128) throw Invalid("数字过长");
                    }
                }
                Value = new Number(json.Substring(start, Position - start));
            }
        }

        private IEnumerable<int> ReadString()
        {
            Take('"');
            var text = new StringBuilder();
            int count = 0;
            while (true)
            {
                if (Position >= json.Length) throw Invalid("字符串没有结束引号");
                char character = json[Position++];
                if (character == '"') break;
                if (character < 32) throw Invalid("字符串中存在未转义的控制字符");
                if (character == '\\')
                {
                    if (Position >= json.Length) throw Invalid("转义字符不完整");
                    switch (json[Position++])
                    {
                        case '"': character = '"'; break;
                        case '\\': character = '\\'; break;
                        case '/': character = '/'; break;
                        case 'b': character = '\b'; break;
                        case 'f': character = '\f'; break;
                        case 'n': character = '\n'; break;
                        case 'r': character = '\r'; break;
                        case 't': character = '\t'; break;
                        case 'u':
                            if (Position + 4 > json.Length || !ushort.TryParse(json.Substring(Position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code))
                                throw Invalid("Unicode 转义需要四位十六进制数字");
                            character = (char)code;
                            Position += 4;
                            break;
                        default: throw Invalid("不支持的转义字符");
                    }
                }
                text.Append(character);
                if (++count == 128) { count = 0; yield return 0; }
            }
            Value = text.ToString();
            yield return 0;
        }

        private IEnumerable<int> WhiteSpace()
        {
            int count = 0;
            while (Peek() == ' ' || Peek() == '\t' || Peek() == '\r' || Peek() == '\n')
            {
                Position++;
                if (++count == 128) { count = 0; yield return 0; }
            }
        }

        private char Peek() { return Position < json.Length ? json[Position] : '\0'; }
        private static bool Digit(char value) { return value >= '0' && value <= '9'; }
        private void Take(char expected)
        {
            if (Peek() != expected) throw Invalid("需要字符 " + expected);
            Position++;
        }

        private void Literal(string text)
        {
            if (Position + text.Length > json.Length || string.CompareOrdinal(json, Position, text, 0, text.Length) != 0)
                throw Invalid("需要 " + text);
            Position += text.Length;
        }

        private InvalidDataException Invalid(string reason)
        {
            return new InvalidDataException("JSON 解析失败，字符位置 " + Position + "：" + reason + "。");
        }
    }
}
