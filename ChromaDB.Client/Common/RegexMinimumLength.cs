namespace ChromaDB.Client.Common;

// The minimum length of a match of a pattern, in characters, as the Rust crate regex reads it, the one of Chroma: its syntax at
// https://docs.rs/regex/1.12.3/regex/#syntax, read as its parser regex-syntax 0.8 reads it. Anchors and word boundaries match no character,
// a class or an escaped character one, and a repetition the minimum of its count. Null for a pattern that the client cannot read with that
// syntax: Chroma rejects it too.
internal static class RegexMinimumLength
{
	// The deepest nesting of groups and classes that regex-syntax accepts: a pattern nested deeper is one that Chroma rejects.
	private const int MaxDepth = 250;

	private static readonly string[] AsciiClasses =
		["alnum", "alpha", "ascii", "blank", "cntrl", "digit", "graph", "lower", "print", "punct", "space", "upper", "word", "xdigit"];

	public static int? Of(string pattern)
	{
		var parser = new Parser(pattern);
		try
		{
			var length = parser.Alternation();
			return parser.AtEnd ? length : null;
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private sealed class Parser(string pattern)
	{
		private int _position;
		private int _depth;
		// The flag x: blanks and # comments do not count, until the end of the group that sets it.
		private bool _verbose;

		public bool AtEnd
		{
			get
			{
				SkipBlanks();
				return _position == pattern.Length;
			}
		}

		private char? Peek() => _position < pattern.Length ? pattern[_position] : null;

		private char Next() => _position < pattern.Length ? pattern[_position++] : throw new FormatException();

		private bool Take(string text)
		{
			if (string.CompareOrdinal(pattern, _position, text, 0, text.Length) != 0)
			{
				return false;
			}
			_position += text.Length;
			return true;
		}

		private void SkipBlanks()
		{
			while (_verbose && Peek() is { } c)
			{
				if (char.IsWhiteSpace(c))
				{
					_position++;
				}
				else if (c == '#')
				{
					while (Peek() is { } comment && comment != '\n')
					{
						_position++;
					}
				}
				else
				{
					return;
				}
			}
		}

		// The next character after the one at the position, past the blanks in verbose mode.
		private char? PeekAfter()
		{
			var position = _position;
			_position++;
			SkipBlanks();
			var c = Peek();
			_position = position;
			return c;
		}

		private void Enter()
		{
			if (++_depth > MaxDepth)
			{
				throw new FormatException();
			}
		}

		// a|b|c: the shortest branch, and an empty branch matches no character.
		public int Alternation()
		{
			var length = Concatenation();
			while (Peek() == '|')
			{
				_position++;
				length = Math.Min(length, Concatenation());
			}
			return length;
		}

		private int Concatenation()
		{
			var length = 0;
			while (true)
			{
				SkipBlanks();
				if (Peek() is not { } c || c == '|' || c == ')')
				{
					return length;
				}
				length = Add(length, Repetition());
			}
		}

		private int Repetition()
		{
			var length = Atom();
			while (true)
			{
				SkipBlanks();
				switch (Peek())
				{
					case '*' or '?':
						_position++;
						length = 0;
						break;
					case '+':
						_position++;
						break;
					case '{':
						_position++;
						SkipBlanks();
						var minimum = Number();
						if (Peek() == ',')
						{
							_position++;
							SkipBlanks();
							if (Peek() != '}' && Number() < minimum)
							{
								throw new FormatException();
							}
						}
						if (Next() != '}')
						{
							throw new FormatException();
						}
						length = Multiply(length, minimum);
						// The lazy form of a count may come after blanks in verbose mode.
						SkipBlanks();
						break;
					default:
						return length;
				}
				// The lazy form, like x*?, has the same minimum. After *, + and ? it is right after them: in verbose mode x+ ? is (x+)?.
				if (Peek() == '?')
				{
					_position++;
				}
			}
		}

		// A count: blanks around it are allowed also out of verbose mode, and between its digits in verbose mode.
		private int Number()
		{
			while (Peek() is { } blank && char.IsWhiteSpace(blank))
			{
				_position++;
			}
			var number = 0L;
			var digits = 0;
			while (Peek() is >= '0' and <= '9' and var digit)
			{
				number = Math.Min(number * 10 + (digit - '0'), (long)uint.MaxValue + 1);
				digits++;
				_position++;
				SkipBlanks();
			}
			while (Peek() is { } blank && char.IsWhiteSpace(blank))
			{
				_position++;
				SkipBlanks();
			}
			return digits > 0 && number <= uint.MaxValue ? (int)Math.Min(number, int.MaxValue) : throw new FormatException();
		}

		private int Atom()
		{
			var c = Next();
			switch (c)
			{
				case '^' or '$':
					return 0;
				case '(':
					return Group();
				case '[':
					Class();
					return 1;
				case '\\':
					return Escape(inClass: false);
				case '*' or '+' or '?' or '{' or ')' or '|':
					throw new FormatException();
				default:
					// A surrogate pair is one character.
					if (char.IsHighSurrogate(c) && Peek() is { } low && char.IsLowSurrogate(low))
					{
						_position++;
					}
					return 1;
			}
		}

		// (exp), (?:exp), (?P<name>exp), (?<name>exp), (?flags) and (?flags:exp). The flags of a group last until its end; (?flags) sets
		// them for the rest of the group around it.
		private int Group()
		{
			Enter();
			var verbose = _verbose;
			SkipBlanks();
			if (Take("?=") || Take("?!") || Take("?<=") || Take("?<!"))
			{
				throw new FormatException();
			}
			if (Take("?P<") || Take("?<"))
			{
				if (Peek() is not { } first || !(first == '_' || char.IsLetter(first)))
				{
					throw new FormatException();
				}
				while (Peek() is { } n && (char.IsLetterOrDigit(n) || n == '_' || n == '.' || n == '[' || n == ']'))
				{
					_position++;
				}
				if (Next() != '>')
				{
					throw new FormatException();
				}
			}
			else if (Take("?"))
			{
				var flags = _verbose;
				var negated = false;
				var dangling = false;
				var start = _position;
				while (Peek() is { } flag && flag != ':' && flag != ')')
				{
					if (flag == '-' && !negated)
					{
						negated = true;
					}
					else if (flag == 'x')
					{
						flags = !negated;
					}
					else if ("imsRUu".IndexOf(flag) < 0)
					{
						throw new FormatException();
					}
					dangling = flag == '-';
					_position++;
				}
				// A negation with no flag after it is not a group, nor (?) with no flag.
				var empty = _position == start;
				if (dangling || (Next() == ')' && empty))
				{
					throw new FormatException();
				}
				if (pattern[_position - 1] == ')')
				{
					// (?flags) matches no character.
					_verbose = flags;
					_depth--;
					return 0;
				}
				_verbose = flags;
			}
			var length = Alternation();
			_verbose = verbose;
			_depth--;
			return Next() == ')' ? length : throw new FormatException();
		}

		// [xyz], [^xyz], ranges, [[:alpha:]], nested classes, &&, -- and ~~: a class matches one character, so only its end matters.
		private void Class()
		{
			Enter();
			SkipBlanks();
			if (Take("^"))
			{
				SkipBlanks();
			}
			// Any '-' at the start is a character of the class; a ']' right after '[' or '[^' too.
			var dashes = false;
			while (Take("-"))
			{
				dashes = true;
				SkipBlanks();
			}
			if (!dashes && Take("]"))
			{
				SkipBlanks();
			}
			while (true)
			{
				SkipBlanks();
				switch (Next())
				{
					case ']':
						_depth--;
						return;
					case '[':
						if (!AsciiClass())
						{
							Class();
						}
						break;
					case '&' when Peek() == '&':
					case '-' when Peek() == '-':
					case '~' when Peek() == '~':
						_position++;
						break;
					default:
						_position--;
						Range();
						break;
				}
			}
		}

		// [:alpha:] and [:^alpha:] inside a class, with one of the names of the syntax; otherwise the '[' opens a nested class.
		private bool AsciiClass()
		{
			var start = _position;
			if (Take(":"))
			{
				Take("^");
				var name = _position;
				while (Peek() is { } c && c != ':')
				{
					_position++;
				}
				if (Array.IndexOf(AsciiClasses, pattern.Substring(name, _position - name)) >= 0 && Take(":]"))
				{
					return true;
				}
			}
			_position = start;
			return false;
		}

		// A character or an escape, or a range of two: a '-' before ']' or another '-' is not a range.
		private void Range()
		{
			Item();
			SkipBlanks();
			if (Peek() == '-' && PeekAfter() is not (']' or '-'))
			{
				_position++;
				SkipBlanks();
				Item();
			}
		}

		private void Item()
		{
			if (Next() == '\\')
			{
				Escape(inClass: true);
			}
		}

		private int Escape(bool inClass)
		{
			var c = Next();
			switch (c)
			{
				case 'x':
					Hex(2);
					return 1;
				case 'u':
					Hex(4);
					return 1;
				case 'U':
					Hex(8);
					return 1;
				case 'p' or 'P':
					SkipBlanks();
					if (Take("{"))
					{
						while (true)
						{
							SkipBlanks();
							if (Next() == '}')
							{
								break;
							}
						}
					}
					else if (Next() == '\\')
					{
						throw new FormatException();
					}
					return 1;
				case 'd' or 'D' or 'w' or 'W' or 's' or 'S':
					return 1;
				// Any ASCII character other than a letter, a digit, '<' and '>' is a character when escaped, like \* or \[ or an escaped
				// blank.
				case < (char)128 when !char.IsLetterOrDigit(c) && c != '<' && c != '>':
					return 1;
				case 'a' or 'f' or 't' or 'n' or 'r' or 'v':
					return 1;
				// Assertions: no character, and not in a class.
				case 'A' or 'z' or 'B' or '<' or '>' when !inClass:
					return 0;
				case 'b' when !inClass:
					SpecialWordBoundary();
					return 0;
				default:
					// Octal escapes and backreferences, and the letters of no escape, which Chroma does not read either.
					throw new FormatException();
			}
		}

		// \b{start}, \b{end}, \b{start-half} and \b{end-half}. A '{' that does not start with a letter or '-' is a count of \b, as \b{2}.
		private void SpecialWordBoundary()
		{
			var start = _position;
			if (!Take("{"))
			{
				return;
			}
			SkipBlanks();
			if (Peek() is not (>= 'a' and <= 'z') and not (>= 'A' and <= 'Z') and not '-')
			{
				_position = start;
				return;
			}
			var name = new System.Text.StringBuilder();
			while (Peek() is { } c && c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-')
			{
				name.Append(c);
				_position++;
				SkipBlanks();
			}
			if (Next() != '}' || name.ToString() is not ("start" or "end" or "start-half" or "end-half"))
			{
				throw new FormatException();
			}
		}

		// \x7F, \x{10FFFF} and the same for \u and \U: a code point that is not a surrogate.
		private void Hex(int digits)
		{
			SkipBlanks();
			var value = 0L;
			if (Take("{"))
			{
				var count = 0;
				while (true)
				{
					SkipBlanks();
					var c = Next();
					if (c == '}')
					{
						break;
					}
					value = Math.Min(value * 16 + HexValue(c), 0x110000);
					count++;
				}
				if (count == 0)
				{
					throw new FormatException();
				}
			}
			else
			{
				for (var i = 0; i < digits; i++)
				{
					if (i > 0)
					{
						SkipBlanks();
					}
					value = Math.Min(value * 16 + HexValue(Next()), 0x110000);
				}
			}
			if (value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF)
			{
				throw new FormatException();
			}
		}

		private static int HexValue(char c)
			=> c switch
			{
				>= '0' and <= '9' => c - '0',
				>= 'a' and <= 'f' => c - 'a' + 10,
				>= 'A' and <= 'F' => c - 'A' + 10,
				_ => throw new FormatException(),
			};

		private static int Add(int a, int b) => a > int.MaxValue - b ? int.MaxValue : a + b;

		private static int Multiply(int a, int b) => a == 0 || b == 0 ? 0 : a > int.MaxValue / b ? int.MaxValue : a * b;
	}
}
