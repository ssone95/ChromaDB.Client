using ChromaDB.Client.Common;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// Every element of the syntax of the Rust crate regex 1.12, the one of Chroma (https://docs.rs/regex/1.12.3/regex/#syntax), with the
// minimum length of a match in characters: 0 for anchors, boundaries and repetitions that may take nothing, null for what the client does
// not read. On Chroma Cloud a pattern of minimum length 0 finds every record (KD-56). Each value is the one of regex-syntax 0.8.10, the
// parser of Chroma, whose minimum length is in bytes: it is 0 for the same patterns.
[TestFixture]
public class RegexMinimumLengthTests
{
	// Matching one character.
	[TestCase(".", 1)]
	[TestCase("[0-9]", 1)]
	[TestCase("\\d", 1)]
	[TestCase("\\D", 1)]
	[TestCase("\\pL", 1)]
	[TestCase("\\p{Greek}", 1)]
	[TestCase("\\PL", 1)]
	[TestCase("\\P{Greek}", 1)]
	// Character classes.
	[TestCase("[xyz]", 1)]
	[TestCase("[^xyz]", 1)]
	[TestCase("[a-z]", 1)]
	[TestCase("[[:alpha:]]", 1)]
	[TestCase("[[:^alpha:]]", 1)]
	[TestCase("[x[^xyz]]", 1)]
	[TestCase("[a-y&&xyz]", 1)]
	[TestCase("[0-9&&[^4]]", 1)]
	[TestCase("[0-9--4]", 1)]
	[TestCase("[a-g~~b-h]", 1)]
	[TestCase("[\\[\\]]", 1)]
	// An empty class: no character matches it, and Chroma rejects it.
	[TestCase("[a&&b]", 1)]
	// A ']' right after '[' or '[^' is a character; after a '-' it closes the class.
	[TestCase("[]a]", 1)]
	[TestCase("[^]a]", 1)]
	[TestCase("[]a[:alpha:]]*", 0)]
	[TestCase("[-]]", 2)]
	// A '[' that ends a range is a character, and [:x:] with a name that is not of an ASCII class is a nested class.
	[TestCase("[!-[]", 1)]
	[TestCase("[!-[]|", 0)]
	[TestCase("[[:x]]", 1)]
	[TestCase("[[:x]a:]]", 2)]
	// Composites.
	[TestCase("xy", 2)]
	[TestCase("x|y", 1)]
	[TestCase("x|", 0)]
	[TestCase("apple|pie", 3)]
	// Repetitions, greedy and lazy.
	[TestCase("x*", 0)]
	[TestCase("x+", 1)]
	[TestCase("x?", 0)]
	[TestCase("x*?", 0)]
	[TestCase("x+?", 1)]
	[TestCase("x??", 0)]
	[TestCase("x{2,3}", 2)]
	[TestCase("x{0,3}", 0)]
	[TestCase("x{2,}", 2)]
	[TestCase("x{0,}", 0)]
	[TestCase("x{2}", 2)]
	[TestCase("x{0}", 0)]
	[TestCase("x{2,3}?", 2)]
	[TestCase("x{2,}?", 2)]
	[TestCase("x{2}?", 2)]
	[TestCase("ax*", 1)]
	[TestCase("[a-z]+", 1)]
	// Empty matches: anchors and boundaries take no character.
	[TestCase("^", 0)]
	[TestCase("$", 0)]
	[TestCase("^$", 0)]
	[TestCase("^a$", 1)]
	[TestCase("\\A", 0)]
	[TestCase("\\z", 0)]
	[TestCase("\\b", 0)]
	[TestCase("\\bapple\\b", 5)]
	[TestCase("\\B", 0)]
	[TestCase("\\b{start}", 0)]
	[TestCase("\\<", 0)]
	[TestCase("\\b{end}", 0)]
	[TestCase("\\>", 0)]
	[TestCase("\\b{start-half}", 0)]
	[TestCase("\\b{end-half}x", 1)]
	// Grouping and flags.
	[TestCase("(apple)", 5)]
	[TestCase("()", 0)]
	[TestCase("(?P<n>apple)", 5)]
	[TestCase("(?P<n>x*)", 0)]
	[TestCase("(?<n>apple)", 5)]
	[TestCase("(?:apple)", 5)]
	[TestCase("(?:)", 0)]
	[TestCase("(?i)apple", 5)]
	[TestCase("(?i)", 0)]
	[TestCase("(?i:apple)", 5)]
	[TestCase("(?m)^$", 0)]
	[TestCase("(?s).", 1)]
	[TestCase("(?mR)^x", 1)]
	[TestCase("(?U)a+", 1)]
	[TestCase("(?U)a*", 0)]
	[TestCase("(?-u:a)", 1)]
	// The flag x: blanks and # comments do not count, until the end of the group that sets it; an escaped blank does. After *, + and ? the
	// lazy ? comes right after them, so x+ ? is (x+)?, while after a count it may come after blanks.
	[TestCase("(?x)apple", 5)]
	[TestCase("(?x) a p p l e  # comment", 5)]
	[TestCase("(?x)a\\ b", 3)]
	[TestCase("(?x)a\\#b", 3)]
	[TestCase("(?x)[ a ]", 1)]
	[TestCase("(?x) a* ", 0)]
	[TestCase("(?x)a+ ?", 0)]
	[TestCase("(?x)a+?", 1)]
	[TestCase("(?x)a{ 2 } ?", 2)]
	[TestCase("(?x)a #c\n+", 1)]
	[TestCase("(?ix:a)", 1)]
	[TestCase("(?x:a b)c d", 5)]
	[TestCase("(?x)(?-x) a", 2)]
	[TestCase("(a(?x) b) c", 4)]
	// Escape sequences.
	[TestCase("\\*", 1)]
	[TestCase("\\a", 1)]
	[TestCase("\\f", 1)]
	[TestCase("\\t", 1)]
	[TestCase("\\n", 1)]
	[TestCase("\\r", 1)]
	[TestCase("\\v", 1)]
	[TestCase("\\x7F", 1)]
	[TestCase("\\x{10FFFF}", 1)]
	[TestCase("\\u007F", 1)]
	[TestCase("\\u{7F}", 1)]
	[TestCase("\\U0000007F", 1)]
	[TestCase("\\U{7F}", 1)]
	[TestCase("\\p{Letter}", 1)]
	[TestCase("\\P{Letter}", 1)]
	// Perl character classes.
	[TestCase("\\s", 1)]
	[TestCase("\\S", 1)]
	[TestCase("\\w", 1)]
	[TestCase("\\W", 1)]
	// ASCII character classes.
	[TestCase("[[:alnum:]]", 1)]
	[TestCase("[[:ascii:]]", 1)]
	[TestCase("[[:blank:]]", 1)]
	[TestCase("[[:cntrl:]]", 1)]
	[TestCase("[[:digit:]]", 1)]
	[TestCase("[[:graph:]]", 1)]
	[TestCase("[[:lower:]]", 1)]
	[TestCase("[[:print:]]", 1)]
	[TestCase("[[:punct:]]", 1)]
	[TestCase("[[:space:]]", 1)]
	[TestCase("[[:upper:]]", 1)]
	[TestCase("[[:word:]]", 1)]
	[TestCase("[[:xdigit:]]", 1)]
	// A character outside the BMP is one character.
	[TestCase("😀", 1)]
	// The empty pattern.
	[TestCase("", 0)]
	// A count of \b, not a special boundary; blanks around a count, also out of verbose mode.
	[TestCase("\\b{2}", 0)]
	[TestCase("x{ 2 }", 2)]
	[TestCase("x{ 2 , 3 }", 2)]
	// What Chroma rejects, and the client does not read: octal escapes and backreferences, look-arounds, \Z, a repetition without its
	// minimum, an unclosed group or class, a repetition of nothing, flags with no flag or a negation at the end, an escape that is not a
	// code point, an assertion in a class, and a count lower than its minimum.
	[TestCase("\\1", null)]
	[TestCase("(?=x)", null)]
	[TestCase("(?!x)", null)]
	[TestCase("(?<=x)", null)]
	[TestCase("(?<!x)", null)]
	[TestCase("\\Z", null)]
	[TestCase("x{,3}", null)]
	[TestCase("x{3,2}", null)]
	[TestCase("(x", null)]
	[TestCase("[x", null)]
	[TestCase("*x", null)]
	[TestCase("x)", null)]
	[TestCase("(?)", null)]
	[TestCase("(?i-)", null)]
	[TestCase("(?-:a)", null)]
	[TestCase("(?P<>a)", null)]
	[TestCase("(?P<1>a)", null)]
	[TestCase("\\x{D800}", null)]
	[TestCase("\\u{110000}", null)]
	[TestCase("[\\b]", null)]
	[TestCase("[\\A]", null)]
	[TestCase("\\b{x}", null)]
	[TestCase("\\p\\", null)]
	[TestCase("\\é", null)]
	public void MinimumLength(string pattern, int? expected)
		=> Assert.That(RegexMinimumLength.Of(pattern), Is.EqualTo(expected));

	// Nesting deeper than regex-syntax accepts, which the client does not read either, and without running out of stack.
	[Test]
	public void DeepNesting()
	{
		Assert.That(RegexMinimumLength.Of(new string('(', 200) + "a" + new string(')', 200)), Is.EqualTo(1));
		Assert.That(RegexMinimumLength.Of(new string('(', 100_000) + "a" + new string(')', 100_000)), Is.Null);
		Assert.That(RegexMinimumLength.Of(new string('[', 100_000) + "a" + new string(']', 100_000)), Is.Null);
	}
}
