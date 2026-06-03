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
    public Envir env;
    public ChemicalReactionRuntime reaction;

    public float GetAmount(string substanceId)
    {
        if (env == null || string.IsNullOrEmpty(substanceId))
            return 0f;
        return env.GetChemicalAmount(ChemistrySystem.GetSubstanceIndex(substanceId));
    }

    public float GetReactantCoeff(string substanceId)
    {
        return ChemistrySystem.GetTermCoeff(reaction, substanceId, reactant: true);
    }

    public float GetProductCoeff(string substanceId)
    {
        return ChemistrySystem.GetTermCoeff(reaction, substanceId, reactant: false);
    }
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
        Node root = parser.Parse().Optimize();
        return new ChemistryExpression(source, root);
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

        // 编译期优化：返回等价但更快的节点（常量折叠等）。默认不变。
        public virtual Node Optimize() => this;

        // 是否为编译期常量；若是，输出其值。用于常量折叠。
        public virtual bool TryGetConstant(out float value)
        {
            value = 0f;
            return false;
        }
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

        public override bool TryGetConstant(out float value)
        {
            value = this.value;
            return true;
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
                case "amount": return context.GetAmount(key);
                case "reactantCoeff": return context.GetReactantCoeff(key);
                case "productCoeff": return context.GetProductCoeff(key);
                default: throw new InvalidOperationException("未知变量: " + name);
            }
        }

        public override Node Optimize()
        {
            if (name == "true") return new NumberNode(1f);
            if (name == "false") return new NumberNode(0f);
            return this;
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

        public override Node Optimize()
        {
            Node optimizedInner = inner.Optimize();
            if (optimizedInner.TryGetConstant(out _))
                return new NumberNode(new UnaryNode(op, optimizedInner).Evaluate(default));
            return new UnaryNode(op, optimizedInner);
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

        public override Node Optimize()
        {
            Node optimizedLeft = left.Optimize();
            Node optimizedRight = right.Optimize();
            if (optimizedLeft.TryGetConstant(out _) && optimizedRight.TryGetConstant(out _))
                return new NumberNode(new BinaryNode(op, optimizedLeft, optimizedRight).Evaluate(default));
            return new BinaryNode(op, optimizedLeft, optimizedRight);
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

        public override Node Optimize()
        {
            int expectedArgs = name switch
            {
                "abs" => 1,
                "min" => 2,
                "max" => 2,
                "pow" => 2,
                "clamp" => 3,
                _ => throw new InvalidOperationException("未知函数: " + name)
            };
            if (args.Count != expectedArgs)
                throw new InvalidOperationException(name + " 需要 " + expectedArgs + " 个参数");

            List<Node> optimizedArgs = new List<Node>(args.Count);
            bool allConst = true;
            for (int i = 0; i < args.Count; i++)
            {
                Node optimizedArg = args[i].Optimize();
                optimizedArgs.Add(optimizedArg);
                if (!optimizedArg.TryGetConstant(out _))
                    allConst = false;
            }

            if (allConst)
                return new NumberNode(new FunctionNode(name, optimizedArgs).Evaluate(default));

            if (name == "pow" && optimizedArgs[1].TryGetConstant(out float exponent))
                return new PowConstNode(optimizedArgs[0], exponent);

            return new FunctionNode(name, optimizedArgs);
        }
    }

    // pow(base, 常量指数) 特化：整数指数用乘法，0.5 用 Sqrt，其余缓存指数后用 Math.Pow，
    // 避免每次求值都遍历指数子树和做参数数量检查。
    class PowConstNode : Node
    {
        readonly Node baseNode;
        readonly float exponent;
        readonly int intExponent;
        readonly bool useIntMultiply;
        readonly bool useSqrt;

        public PowConstNode(Node baseNode, float exponent)
        {
            this.baseNode = baseNode;
            this.exponent = exponent;

            useSqrt = exponent == 0.5f;
            int rounded = (int)exponent;
            useIntMultiply = !useSqrt && rounded == exponent && rounded >= 0 && rounded <= 4;
            intExponent = rounded;
        }

        public override float Evaluate(ChemistryExpressionContext context)
        {
            float b = baseNode.Evaluate(context);

            if (useSqrt)
                return b <= 0f ? 0f : (float)Math.Sqrt(b);

            if (useIntMultiply)
            {
                switch (intExponent)
                {
                    case 0: return 1f;
                    case 1: return b;
                    case 2: return b * b;
                    case 3: return b * b * b;
                    default:
                        float square = b * b;
                        return square * square;
                }
            }

            return (float)Math.Pow(b, exponent);
        }

        public override Node Optimize() => this;
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
                if (!IsAllowedFunction(identifier))
                    throw new InvalidOperationException("未知函数: " + identifier);

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
            ValidateVariable(identifier, key);
            return new VariableNode(identifier, key);
        }

        static bool IsAllowedFunction(string identifier)
        {
            return identifier == "abs"
                || identifier == "min"
                || identifier == "max"
                || identifier == "pow"
                || identifier == "clamp";
        }

        static void ValidateVariable(string identifier, string key)
        {
            switch (identifier)
            {
                case "tempC":
                case "light":
                case "height":
                case "topography":
                case "limiting":
                case "true":
                case "false":
                    if (key != null)
                        throw new InvalidOperationException(identifier + " 不支持索引");
                    return;
                case "amount":
                case "reactantCoeff":
                case "productCoeff":
                    if (string.IsNullOrWhiteSpace(key))
                        throw new InvalidOperationException(identifier + " 需要字符串索引，例如 " + identifier + "[\"organic\"]");
                    return;
                default:
                    throw new InvalidOperationException("未知变量: " + identifier);
            }
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
