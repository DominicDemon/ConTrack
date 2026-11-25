import 'package:flutter/material.dart';

/// The vault search as the screen shows it: the same rules as the server (VaultSearch.cs), so what is highlighted is
/// what made a document match. Accents, case, spaces and punctuation do not matter; a phone typed +84… also means 0….
class VaultSearch {
  VaultSearch._();

  static const _groups = {
    'a': 'àáạảãâầấậẩẫăằắặẳẵ', 'e': 'èéẹẻẽêềếệểễ', 'i': 'ìíịỉĩ', 'o': 'òóọỏõôồốộổỗơờớợởỡ',
    'u': 'ùúụủũưừứựửữ', 'y': 'ỳýỵỷỹ', 'd': 'đ',
  };
  static final Map<String, String> _plain = {
    for (final entry in _groups.entries) for (final c in entry.value.split('')) c: entry.key,
  };
  static final _wordChar = RegExp(r'[\p{L}\p{N}]', unicode: true);
  static final _mark = RegExp(r'[̀-ͯ]');

  /// One character without accents, lower case ('' for a combining accent).
  static String _fold(String c) {
    if (_mark.hasMatch(c)) return '';
    final lower = c.toLowerCase();
    return _plain[lower] ?? lower;
  }

  /// Lower case, no accents, split on anything that is not a letter or a digit.
  static List<String> words(String? value) => (value ?? '').split('')
      .map((c) { final p = _fold(c); return p.isNotEmpty && _wordChar.hasMatch(p) ? p : ' '; })
      .join().split(' ').where((w) => w.isNotEmpty).toList();

  static List<String> _variants(String word) =>
      RegExp(r'^84\d{8,10}$').hasMatch(word) ? [word, '0${word.substring(2)}'] : [word];

  /// The whole query without spaces, then each word of 2 characters or more.
  static List<String> _needles(String query) {
    final ws = words(query);
    final whole = ws.join();
    return {if (whole.length >= 2) ..._variants(whole), for (final w in ws.where((w) => w.length >= 2)) ..._variants(w)}.toList();
  }

  /// Whether a field (a keyword value, the title…) contains part of the query.
  static bool fieldMatches(String? value, String query) {
    final compact = words(value).join();
    return compact.isNotEmpty && _needles(query).any(compact.contains);
  }

  /// The text with the parts that match the query highlighted.
  static TextSpan highlight(String? text, String query, {TextStyle? style, Color? mark}) {
    final value = text ?? '';
    final found = _needles(query);
    if (value.isEmpty || found.isEmpty) return TextSpan(text: value, style: style);
    final chars = value.split('');
    final compact = StringBuffer(); final at = <int>[];
    for (var i = 0; i < chars.length; i++) {
      final p = _fold(chars[i]);
      if (p.isNotEmpty && _wordChar.hasMatch(p)) { compact.write(p); at.add(i); }
    }
    final flat = compact.toString();
    final marked = List<bool>.filled(chars.length, false);
    for (final needle in found) {
      for (var start = flat.indexOf(needle); start >= 0; start = flat.indexOf(needle, start + 1)) {
        for (var i = at[start]; i <= at[start + needle.length - 1]; i++) { marked[i] = true; }
      }
    }
    if (!marked.contains(true)) return TextSpan(text: value, style: style);
    final markStyle = TextStyle(backgroundColor: mark ?? const Color(0xFFFDE68A), color: Colors.black87);
    final parts = <TextSpan>[]; final run = StringBuffer(); var inMark = marked[0];
    void flush() { if (run.isNotEmpty) { parts.add(TextSpan(text: run.toString(), style: inMark ? markStyle : null)); run.clear(); } }
    for (var i = 0; i < chars.length; i++) {
      if (marked[i] != inMark) { flush(); inMark = marked[i]; }
      run.write(chars[i]);
    }
    flush();
    return TextSpan(style: style, children: parts);
  }
}
