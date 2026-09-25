import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// The web app's modules follow the bounded contexts. Each may use only what is listed here, so
// Board Modelling knows nothing of Collaboration (the page plugs presence into the editor's slots),
// and shared code knows nothing of any module.

const root = join(__dirname);

const allowed: Record<string, string[]> = {
  'shared': ['shared', 'store/hooks', 'store/types'],
  'modules/identity': ['shared', 'store', 'modules/identity'],
  'modules/teams': ['shared', 'store', 'modules/teams', 'modules/identity'],
  'modules/public-integration': ['shared', 'store', 'modules/public-integration'],
  'modules/board-modelling': ['shared', 'store', 'modules/board-modelling'],
  'modules/collaboration': ['shared', 'store', 'modules/collaboration', 'modules/board-modelling', 'modules/identity'],
  'store': ['shared', 'store', 'modules'],
  'test': ['shared', 'store', 'modules', 'test'],
  'app': ['shared', 'store', 'modules', 'app'],
};

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

function areaOf(path: string): string {
  const parts = path.split('/');
  return parts[0] === 'modules' ? `modules/${parts[1]}` : parts[0]!;
}

/** Every module specifier: `from 'x'`, side-effect `import 'x'`, and dynamic `import('x')`. */
function specifiersOf(source: string): string[] {
  return [...source.matchAll(/(?:\bfrom\s+|\bimport\s*\(?\s*)['"]([^'"]+)['"]/g)].map((match) => match[1]!);
}

/** Every '@/…' import, as a path under src. */
function importsOf(source: string): string[] {
  return specifiersOf(source)
    .filter((specifier) => specifier.startsWith('@/'))
    .map((specifier) => specifier.slice(2));
}

const files = sourceFiles(root)
  .map((path) => relative(root, path).split(sep).join('/'))
  .filter((path) => !path.endsWith('architecture.test.ts'));

describe('module boundaries', () => {
  it.each(files.filter((path) => !path.startsWith('test/') && !/\.test\.tsx?$/.test(path)))('%s imports only what its module may use', (path) => {
    const area = areaOf(path);
    const rules = allowed[area];
    expect(rules, `No rule for ${area}`).toBeDefined();
    const source = readFileSync(join(root, path), 'utf8');
    const refused = importsOf(source).filter((target) => !rules!.some((rule) => target === rule || target.startsWith(`${rule}/`)));
    expect(refused).toEqual([]);
  });

  it('never reaches into another module through a relative path', () => {
    const escapes = files.filter((path) => {
      const source = readFileSync(join(root, path), 'utf8');
      const depth = path.startsWith('modules/') ? 2 : 1;
      return specifiersOf(source)
        .map((specifier) => /^((?:\.\.\/)+)/.exec(specifier)?.[1] ?? '')
        .some((ups) => ups.length / 3 > path.split('/').length - 1 - depth);
    });
    expect(escapes).toEqual([]);
  });

  it('keeps Board Modelling free of the collaboration features plugged into it', () => {
    const offenders = files.filter((path) => path.startsWith('modules/board-modelling/') && /@\/modules\/collaboration/.test(readFileSync(join(root, path), 'utf8')));
    expect(offenders).toEqual([]);
  });
});
