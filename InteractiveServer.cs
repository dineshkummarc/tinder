using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;

public class InteractiveServer
{
	private const int port = 8080;
	private const string htmlExample = htmlVector;
	private const string htmlLinkList = @"
external {
  void print(string text)
  string str(int x)
}

class Link {
  int value
  Link next
}

Link cons(int value, Link next) {
  Link link = Link()
  link.value = value
  link.next = next
  return link
}

string printHelper(Link link) {
  if link != null {
    string text = str(link.value)
    if link.next != null { text = text + "", "" }
    return text + printHelper(link.next)
  }
  return """"
}

void print(Link link) {
  print(""["" + printHelper(link) + ""]"")
}

void main() {
  Link list = cons(1, cons(2, cons(3, null)))
  print(list)
}
";
	private const string htmlVector = @"
external {
  void print(string text)
  string str(float x)
}

class Vector {
  float x, y

  static Vector new(float x, float y) {
    Vector v = Vector()
    v.x = x
    v.y = y
    return v
  }

  static Vector new(Vector o) {
    Vector v = Vector()
    v.x = o.x
    v.y = o.y
    return v
  }

  Vector plus(Vector vec) {
    return Vector.new(this.x + vec.x, this.y + vec.y)
  }
}

string str(Vector v) {
  return ""Vector("" + str(v.x) + "", "" + str(v.y) + "")""
}

Vector a

void main() {
  a = Vector.new(1, 2)
  Vector b = Vector.new(3, 4)
  Vector c = Vector.new(a.plus(b))
  print(str(a) + "" + "" + str(b) + "" = "" + str(c))
}
";
	private const string html = @"
		<!DOCTYPE html>
		<html><head>
			<title>Interactive Compiler</title>
		</head><body>
			<style>
				body { font: 13px Arial; margin: 30px; }
				textarea, pre { font: 12px Inconsolata, Consolas, monospace; }
			</style>
			<textarea id='input' rows='20' cols='100'>%s</textarea>
			<div id='output'></div>
			<script>

				var input = document.getElementById('input');
				var output = document.getElementById('output');
				var latest = 0;

				function text2html(text) {
					return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
				}

				function ajaxCompile(text, successFunc) {
					latest = text;
					var request = new XMLHttpRequest();
					request.onreadystatechange = function() {
						if (request.readyState == 4 && latest == text) {
							successFunc(request.responseXML.firstChild);
						}
					};
					request.open('POST', '/compile', true);
					request.send(text);
				}

				input.oninput = function() {
					ajaxCompile(input.value, function(xml) {
						var html = '';
						for (var node = xml.firstChild; node; node = node.nextSibling) {
							var contents = '';
							for (var child = node.firstChild; child; child = child.nextSibling) {
								contents += child.textContent + '\n';
							}
							if (contents.length) {
								html += '<br><br><b>' + text2html(node.tagName) + '</b><br><pre>' + text2html(contents) + '</pre>';
							}
						}
						output.innerHTML = html;
					});
				};

				input.focus();
				input.oninput();

			</script>
		</body></html>
	";
	private HttpListener server;
	
	public void Serve()
	{
		string host = "http://localhost:" + port + "/";
		server = new HttpListener();
		server.Prefixes.Add(host);
		server.Start();
		Console.WriteLine("serving on " + host);
		while (true) {
			HttpListenerContext context = server.GetContext();
			Process(context);
		}
	}
	
	private static void Process(HttpListenerContext context)
	{
		StreamReader input = new StreamReader(context.Request.InputStream);
		StreamWriter output = new StreamWriter(context.Response.OutputStream);
		switch (context.Request.Url.AbsolutePath) {
			case "/":
				context.Response.ContentType = "text/html";
				output.Write(html.Replace("%s", htmlExample));
				output.Close();
				break;
			case "/compile":
				context.Response.ContentType = "text/xml";
				output.Write(Compile(input.ReadToEnd()));
				output.Close();
				break;
		}
	}
	
	private static string Compile(string input)
	{
		Func<XmlDocument, XmlNode, string, XmlNode> createChild = (document, parent, name) => {
			XmlNode node = document.CreateElement(name);
			if (parent != null) {
				parent.AppendChild(node);
			} else {
				document.AppendChild(node);
			}
			return node;
		};
		Action<XmlDocument, XmlNode, string> appendText = (document, parent, text) => {
			parent.AppendChild(document.CreateTextNode(text));
		};
		
		XmlDocument doc = new XmlDocument();
		XmlNode xmlResults = createChild(doc, null, "Results");
		XmlNode xmlWarnings = createChild(doc, xmlResults, "Warnings");
		XmlNode xmlErrors = createChild(doc, xmlResults, "Errors");
		XmlNode xmlJs = createChild(doc, xmlResults, "JavaScript");
		XmlNode xmlTree = createChild(doc, xmlResults, "Tree");
		XmlNode xmlTokens = createChild(doc, xmlResults, "Tokens");
		List<Token> tokens = new List<Token>();
		Module module = null;
		Log log = new Log();
		
		try {
			tokens = Tokenizer.Tokenize(log, "<stdin>", input);
			if (log.errors.Count == 0) {
				module = Parser.Parse(log, tokens, "<stdin>");
				if (module != null) {
					bool compiled = Compiler.Compile(log, module);
					appendText(doc, xmlTree, module.Accept(new NodeToStringVisitor()));
					if (compiled) {
						appendText(doc, xmlJs, JsTarget.Generate(module));
					}
				}
			}
		} catch (Exception e) {
			log.errors.Add(e.ToString());
		}
		
		foreach (string error in log.errors) {
			appendText(doc, createChild(doc, xmlErrors, "Error"), error);
		}
		foreach (string error in log.warnings) {
			appendText(doc, createChild(doc, xmlWarnings, "Warning"), error);
		}
		foreach (Token token in tokens) {
			string text = token.text.Contains("\n") ? token.text.ToQuotedString() : token.text;
			appendText(doc, createChild(doc, xmlTokens, "Token"), token.kind + " " + text);
		}
		
		StringWriter output = new StringWriter();
		doc.WriteTo(new XmlTextWriter(output));
		return output.ToString();
	}
	
	public static void Main(string[] args)
	{
		new InteractiveServer().Serve();
	}
}

public class NodeToStringVisitor : Visitor<string>
{
	private string indent = "";
	
	private void Indent()
	{
		indent += "  ";
	}
	
	private void Dedent()
	{
		indent = indent.Substring(2);
	}
	
	private string Field(string name, string text)
	{
		return indent + name + " = " + (text != null ? text : "null") + "\n";
	}
	
	private string Field(string name, Node node)
	{
		return indent + name + " = " + (node != null ? node.Accept(this) : "null") + "\n";
	}
	
	private string Field(string name, List<string> list)
	{
		if (list.Count == 0) {
			return indent + name + " = {}\n";
		}
		return indent + name + " = { " + list.Join(", ") + " }\n";
	}
	
	private string Field<T>(string name, List<T> nodes) where T : Node
	{
		if (nodes.Count == 0) {
			return indent + name + " = {}\n";
		}
		Indent();
		string items = nodes.ConvertAll(x => indent + x.Accept(this) + "\n").Join();
		Dedent();
		return indent + name + " = {\n" + items + indent + "}\n";
	}
	
	private string Wrap(string name, string fields)
	{
		return name + " {\n" + fields + indent + "}";
	}
	
	public override string Visit(Block node)
	{
		if (node.stmts.Count == 0) {
			return "Block {}";
		}
		string text = "Block {\n";
		Indent();
		foreach (Stmt stmt in node.stmts) {
			text += indent + stmt.Accept(this) + "\n";
		}
		Dedent();
		return text + indent + "}";
	}
	
	public override string Visit(Module node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("block", node.block);
		Dedent();
		return Wrap("Module", fields);
	}
	
	public override string Visit(IfStmt node)
	{
		Indent();
		string fields = Field("test", node.test) + Field("thenBlock", node.thenBlock) + Field("elseBlock", node.elseBlock);
		Dedent();
		return Wrap("IfStmt", fields);
	}
	
	public override string Visit(ReturnStmt node)
	{
		Indent();
		string fields = Field("value", node.value);
		Dedent();
		return Wrap("ReturnStmt", fields);
	}
	
	public override string Visit(ExprStmt node)
	{
		Indent();
		string fields = Field("value", node.value);
		Dedent();
		return Wrap("ExprStmt", fields);
	}
	
	public override string Visit(ExternalStmt node)
	{
		Indent();
		string fields = Field("block", node.block);
		Dedent();
		return Wrap("ExternalStmt", fields);
	}
	
	public override string Visit(WhileStmt node)
	{
		Indent();
		string fields = Field("test", node.test) + Field("block", node.block);
		Dedent();
		return Wrap("WhileStmt", fields);
	}
	
	public override string Visit(VarDef node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("type", node.type) + Field("value", node.value);
		Dedent();
		return Wrap("VarDef", fields);
	}
	
	public override string Visit(FuncDef node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("returnType", node.returnType) +
			Field("argDefs", node.argDefs) + Field("block", node.block);
		Dedent();
		return Wrap("FuncDef", fields);
	}
	
	public override string Visit(ClassDef node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("block", node.block);
		Dedent();
		return Wrap("ClassDef", fields);
	}
	
	public override string Visit(ThisExpr node)
	{
		return "ThisExpr {}";
	}
	
	public override string Visit(NullExpr node)
	{
		return "NullExpr {}";
	}
	
	public override string Visit(BoolExpr node)
	{
		return "BoolExpr { value = " + (node.value ? "true" : "false") + " }";
	}
	
	public override string Visit(IntExpr node)
	{
		return "IntExpr { value = " + node.value + " }";
	}
	
	public override string Visit(FloatExpr node)
	{
		return "FloatExpr { value = " + node.value + " }";
	}
	
	public override string Visit(StringExpr node)
	{
		return "StringExpr { value = " + node.value.ToQuotedString() + " }";
	}
	
	public override string Visit(IdentExpr node)
	{
		return "IdentExpr { name = " + node.name + " }";
	}
	
	public override string Visit(TypeExpr node)
	{
		return "TypeExpr { type = " + node.type + " }";
	}
	
	public override string Visit(ListExpr node)
	{
		string text = "ListExpr {\n";
		Indent();
		foreach (Expr expr in node.items) {
			text += indent + expr.Accept(this) + "\n";
		}
		Dedent();
		return text + indent + "}";
	}
	
	public override string Visit(UnaryExpr node)
	{
		Indent();
		string fields = Field("op", node.op.AsString()) + Field("value", node.value);
		Dedent();
		return Wrap("PrefixExpr", fields);
	}
	
	public override string Visit(BinaryExpr node)
	{
		Indent();
		string fields = Field("left", node.left) + Field("op", node.op.AsString()) + Field("right", node.right);
		Dedent();
		return Wrap("BinaryExpr", fields);
	}
	
	public override string Visit(CallExpr node)
	{
		Indent();
		string fields = Field("func", node.func) + Field("args", node.args);
		Dedent();
		return Wrap("CallExpr", fields);
	}
	
	public override string Visit(ParamExpr node)
	{
		Indent();
		string fields = Field("type", node.type) + Field("typeParams", node.typeParams);
		Dedent();
		return Wrap("ParamExpr", fields);
	}
	
	public override string Visit(CastExpr node)
	{
		Indent();
		string fields = Field("value", node.value) + Field("target", node.target);
		Dedent();
		return Wrap("CastExpr", fields);
	}
	
	public override string Visit(MemberExpr node)
	{
		Indent();
		string fields = Field("obj", node.obj) + Field("name", node.name);
		Dedent();
		return Wrap("MemberExpr", fields);
	}
	
	public override string Visit(IndexExpr node)
	{
		Indent();
		string fields = Field("obj", node.obj) + Field("index", node.index);
		Dedent();
		return Wrap("IndexExpr", fields);
	}
}
