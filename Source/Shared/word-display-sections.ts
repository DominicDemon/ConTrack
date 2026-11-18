type Section = { key: string; title: string; content: string };

const heading = /^(?:Điều\s+\d+(?:\s|[.:–-]|$)|Phụ\s+lục(?:\s|[:–-]|$)|Định\s+nghĩa(?:\s*[:–-]|$))/iu;

export function isGroupedWordSection(term: Section): boolean {
  return /^WORD_BODY_/i.test(term.key) || /^Nội dung Word(?:\s+\d+)?$/iu.test(term.title.trim());
}

/** Present older Word snapshots by heading without changing stored terms or approval data. */
export function wordDisplaySections(terms: readonly Section[]): Section[] {
  return terms.flatMap(term => {
    if (!isGroupedWordSection(term)) return [term];

    const sections: Section[] = [];
    let title = "Phần mở đầu";
    let lines: string[] = [];
    const flush = () => {
      const content = lines.join("\n").trim();
      if (content || title !== "Phần mở đầu") {
        sections.push({ key: `${term.key}-display-${sections.length}`, title, content });
      }
      lines = [];
    };

    for (const line of term.content.split(/\r?\n/)) {
      if (heading.test(line.trim())) {
        flush();
        title = line.trim();
      } else {
        lines.push(line);
      }
    }
    if (sections.length === 0 && title === "Phần mở đầu") return [term];
    flush();
    return sections;
  });
}
