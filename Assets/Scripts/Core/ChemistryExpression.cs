using System;
using System.Collections.Generic;
using System.Globalization;

public struct ChemistryExpressionContext
{
    public float tempC;
    public int light;
    public int height;
    public int topography;
    public float limiting;
    public Func<string, float> AmountOf;
    public Func<string, float> ReactantCoeffOf;
    public Func<string, float> ProductCoeffOf;
}

public class ChemistryExpression
{
    readonly Node root;
    readonly string source;

    ChemistryExpression(string source, Node root)
    {
        this.source = source;
        this.root = root;
    }

    public static ChemistryExpression Compile(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            source = "1";
        Parser parser = new Parser(source);
        return new ChemistryExpression(source, parser.Parse());
    }

    public float Evaluate(ChemistryExpressionContext context)
    {
        float value = root.Evaluate(context);
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return value;
    }

    public override string ToString()
    {
        return source;
    }

    abstract class Node
    {
        public abstract float Evaluate(ChemistryExpressionContext context);
    }

    class NumberNode : Node
    {
        readonly float value;

        public NumberNode(float value)
        {
            this.value = value;
        }

        public override float Evaluate(ChemistryExpressionContext context)
        {
            return value;
        }
    }

    class VariableNode : Node
    {
        readonly string name;
        readonly string key;

        public VariableNode(string name, string key)
        {
            this.name = name;
            this.key = key;
        }

        public override float Evaluate(ChemistryExpressionContext context)
        {
            switch (name)
            {
                case "tempC": return context.tempC;
                case "light": return context.light;
                case "height": return context.height;
                case "topography": return context.topography;
                case "limiting": return context.limiting;
                case "true": return 1f;
                case "false": return 0f;
                case "amount": return context.AmountOf != null ? context.AmountOf(key) : 0f;
                case "reactantCoeff": return context.ReactantCoeffOf != null ? context.ReactantCoeffOf(key) : 0f;
                case "productCoeff": return context.ProductCoeffOf != null ? context.ProductCoeffOf(key) : 0f;
                default: throw new InvalidOperationException("未知变量: " + name);
            }
        }
    }

    class UnaryNode : Node
    {
        readonly string op;
        readonly Node inner;

        public UnaryNode(string op, Node inner)
        {
            this.op = op;
            this.inner = inner;
        }

        public override float Evaluate(ChemistryExpressionContext context)
        {
            float value = inner.Evaluate(context);
            if (op == "-") return -value;
            if (op == "!") return value == 0f ? 1f : 0f;
            return value;
        }
    }

    class BinaryNode : Node
    {
        readonly string op;
        readonly Node left;
        readonly Node right;

        public BinaryNode(string op, Node left, Node right)
        {
            this.op = op;
            this.left = left;
            this.right = right;
        }

        public override float Evaluate(ChemistryExpressionContext context)
        {
            if (op == "&&")
                return left.Evaluate(context) != 0f && right.Evaluate(context) != 0f ? 1f : 0f;
            if (op == "||")
                return left.Evaluate(context) != 0f || right.Evaluate(context) != 0f ? 1f : 0f;

            float a = left.Evaluate(context);
            float b = right.Evaluate(context);
            switch (op)
            {
                case "+": return a + b;
                case "-": return a - b;
                case "*": return a * b;
                case "/": return Math.Abs(b) < 0.000001f ? 0f : a / b;
                case ">": return a > b ? 1f : 0f;
                case ">=": return a >= b ? 1f : 0f;
                case "<": return a < b ? 1f : 0f;
                case "<=": return a <= b ? 1f : 0f;
                case "==": return Math.Abs(a - b) < 0.000001f ? 1f : 0f;
                case "!=": return Math.Abs(a - b) >= 0.000001f ? 1f : 0f;
                default: throw new InvalidOperationException("未知运算符: " + op);
            }
        }
    }

    class FunctionNode : Node
    {
        readonly string name;
        readonly List<Node> args;

        public FunctionNode(string name, List<Node> args)
        {
            this.name = name;
            this.args = args;
        }

        public override float Evaluate(ChemistryExpressionContext context)
        {
            switch (name)
            {
                case "abs": RequireArgCount(1); return Math.Abs(args[0].Evaluate(context));
                case "min": RequireArgCount(2); return Math.Min(args[0].Evaluate(context), args[1].Evaluate(context));
                case "max": RequireArgCount(2); return Math.Max(args[0].Evaluate(context), args[1].Evaluate(context));
                case "pow": RequireArgCount(2); return (float)Math.Pow(args[0].Evaluate(context), args[1].Evaluate(context));
                case "clamp":
                    RequireArgCount(3);
                    float value = args[0].Evaluate(context);
                    float min = args[1].Evaluate(context);
                    float max = args[2].Evaluate(context);
                    if (value < min) return min;
                    if (value > max) return max;
                    return value;
                default:
                    throw new InvalidOperationException("未知函数: " + name);
            }
        }

        void RequireArgCount(int count)
        {
            if (args.Count != count)
                throw new InvalidOperationException(name + " 需要 " + count + " 个参数");
        }
    }

    class Parser
    {
        readonly string text;
        int pos;

        public Parser(string text)
        {
            this.text = text;
        }

        public Node Parse()
        {
            Node node = ParseOr();
            SkipWhitespace();
            if (!IsEnd)
                throw new InvalidOperationException("表达式尾部无法解析: " + text.Substring(pos));
            return node;
        }

        Node ParseOr()
        {
            Node node = ParseAnd();
            while (Match("||"))
                node = new BinaryNode("||", node, ParseAnd());
            return node;
        }

        Node ParseAnd()
        {
            Node node = ParseCompare();
            while (Match("&&"))
                node = new BinaryNode("&&", node, ParseCompare());
            return node;
        }

        Node ParseCompare()
        {
            Node node = ParseAdd();
            while (true)
            {
                if (Match(">=")) node = new BinaryNode(">=", node, ParseAdd());
                else if (Match("<=")) node = new BinaryNode("<=", node, ParseAdd());
                else if (Match("==")) node = new BinaryNode("==", node, ParseAdd());
                else if (Match("!=")) node = new BinaryNode("!=", node, ParseAdd());
                else if (Match(">")) node = new BinaryNode(">", node, ParseAdd());
                else if (Match("<")) node = new BinaryNode("<", node, ParseAdd());
                else return node;
            }
        }

        Node ParseAdd()
        {
            Node node = ParseMul();
            while (true)
            {
                if (Match("+")) node = new BinaryNode("+", node, ParseMul());
                else if (Match("-")) node = new BinaryNode("-", node, ParseMul());
                else return node;
            }
        }

        Node ParseMul()
        {
            Node node = ParseUnary();
            while (true)
            {
                if (Match("*")) node = new BinaryNode("*", node, ParseUnary());
                else if (Match("/")) node = new BinaryNode("/", node, ParseUnary());
                else return node;
            }
        }

        Node ParseUnary()
        {
            if (Match("-")) return new UnaryNode("-", ParseUnary());
            if (Match("!")) return new UnaryNode("!", ParseUnary());
            return ParsePrimary();
        }

        Node ParsePrimary()
        {
            SkipWhitespace();
            if (Match("("))
            {
                Node node = ParseOr();
                Expect(")");
                return node;
            }

            if (!IsEnd && (char.IsDigit(Current) || Current == '.'))
                return ParseNumber();

            string identifier = ParseIdentifier();
            SkipWhitespace();

            if (Match("("))
            {
                List<Node> args = new List<Node>();
                SkipWhitespace();
                if (!Match(")"))
                {
                    do
                    {
                        args.Add(ParseOr());
                    }
                    while (Match(","));
                    Expect(")");
                }
                return new FunctionNode(identifier, args);
            }

            string key = null;
            if (Match("["))
            {
                key = ParseString();
                Expect("]");
            }
            return new VariableNode(identifier, key);
        }

        Node ParseNumber()
        {
            int start = pos;
            while (!IsEnd && (char.IsDigit(Current) || Current == '.' || Current == 'e' || Current == 'E' || Current == '+' || Current == '-'))
            {
                if ((Current == '+' || Current == '-') && pos > start && text[pos - 1] != 'e' && text[pos - 1] != 'E')
                    break;
                pos++;
            }
            string token = text.Substring(start, pos - start);
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                throw new InvalidOperationException("数字格式错误: " + token);
            return new NumberNode(value);
        }

        string ParseIdentifier()
        {
            SkipWhitespace();
            if (IsEnd || !(char.IsLetter(Current) || Current == '_'))
                throw new InvalidOperationException("需要变量或函数名");

            int start = pos;
            while (!IsEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
                pos++;
            return text.Substring(start, pos - start);
        }

        string ParseString()
        {
            SkipWhitespace();
            if (!Match("\""))
                throw new InvalidOperationException("索引需要字符串，例如 amount[\"organic\"]");
            int start = pos;
            while (!IsEnd && Current != '"')
                pos++;
            if (IsEnd)
                throw new InvalidOperationException("字符串缺少结束引号");
            string value = text.Substring(start, pos - start);
            Expect("\"");
            return value;
        }

        bool Match(string token)
        {
            SkipWhitespace();
            if (pos + token.Length > text.Length)
                return false;
            for (int i = 0; i < token.Length; i++)
            {
                if (text[pos + i] != token[i])
                    return false;
            }
            pos += token.Length;
            return true;
        }

        void Expect(string token)
        {
            if (!Match(token))
                throw new InvalidOperationException("需要: " + token);
        }

        void SkipWhitespace()
        {
            while (!IsEnd && char.IsWhiteSpace(Current))
                pos++;
        }

        bool IsEnd => pos >= text.Length;
        char Current => text[pos];
    }
}
