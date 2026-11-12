"""Reads the Fakturoid API changelog page (HTML on stdin) and prints its entries as JSON, newest first:
[{"id": "october-26-2026", "title": "October 26, 2026", "text": "- Added attributes to ..."}, ...]
Used by .github/workflows/upstream-watch.yml."""
import html
import json
import re
import sys

page = sys.stdin.buffer.read().decode('utf-8')

# Each entry: <h2 id="october-26-2026"><a class="anchor-link" ...>October 26, 2026</a></h2> followed by its content.
heading = re.compile(r'<h2 id="([^"]+)">\s*<a class="anchor-link"[^>]*>(.*?)</a>\s*</h2>', re.S)
matches = list(heading.finditer(page))
entries = []
for i, match in enumerate(matches):
    end = matches[i + 1].start() if i + 1 < len(matches) else len(page)
    content = page[match.end():end]
    # The article ends before the next heading of another kind (footer, cookie dialog).
    content = re.split(r'<h2[\s>]|</article>|<footer', content)[0]

    depth = 0
    lines = []
    for token in re.split(r'(<[^>]+>)', content):
        if token.startswith('<'):
            tag = token.lower()
            if tag.startswith('<ul') or tag.startswith('<ol'):
                depth += 1
            elif tag.startswith('</ul') or tag.startswith('</ol'):
                depth = max(depth - 1, 0)
            elif tag.startswith('<li'):
                lines.append('  ' * max(depth - 1, 0) + '- ')
            elif tag in ('<p>', '<br>', '<br/>', '<br />') or tag.startswith('<h3') or tag.startswith('<h4'):
                lines.append('')
            elif tag.startswith('<code') or tag.startswith('</code'):
                lines.append(lines.pop() + '`' if lines else '`')
            continue
        text = re.sub(r'\s+', ' ', html.unescape(token).replace('\xa0', ' '))
        if not lines:
            lines.append('')
        lines[-1] += text
    text = '\n'.join(line.rstrip() for line in lines if line.strip() and line.strip() != '-')
    entries.append({
        'id': match.group(1),
        'title': html.unescape(re.sub(r'<[^>]+>', '', match.group(2))).strip(),
        'text': text,
    })

sys.stdout.buffer.write(json.dumps(entries, ensure_ascii=False, indent=1).encode('utf-8'))
