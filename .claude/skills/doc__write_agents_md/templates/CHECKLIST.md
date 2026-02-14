# AGENTS.md Quality Checklist

Use this checklist to verify your AGENTS.md file meets quality standards.

## Essential Quality Checks

### ✅ Structure
- [ ] Quick Reference section exists with Stack/Docs/Run commands
- [ ] Key Rules section with ✅ good / ❌ bad examples
- [ ] Common Mistakes section with arrows (→) to solutions
- [ ] Project Layout table showing directories and purposes
- [ ] Skills section organized by category
- [ ] Quick Troubleshooting section
- [ ] Documentation Index with links to detailed guides

### ✅ Content Quality
- [ ] File is under 150 lines
- [ ] All commands are copy-pasteable and executable
- [ ] Complex workflows defer to skills (e.g., `/wolverine__create_operation`)
- [ ] Uses third-person voice ("Stack uses .NET" not "You should use")
- [ ] No sensitive information (credentials, API keys, secrets)
- [ ] Good/bad pattern examples are side-by-side
- [ ] Links to detailed docs instead of duplicating content

### ✅ Skills Integration
- [ ] All skill references start with `/` (e.g., `/wolverine__create_operation`)
- [ ] Skills are organized by category (Run, Scaffold, Verify, Debug, Deploy, Utility, Documentation)
- [ ] Skill aliases are documented (e.g., `/sco`→wolverine__create_operation)
- [ ] All referenced skills exist in `.claude/skills/` directory

### ✅ Documentation Links
- [ ] All links to guides use relative paths
- [ ] All linked files exist (no broken links)
- [ ] Documentation Index covers major topics
- [ ] Each guide path is correct (check with file system)

### ✅ Commands
- [ ] Setup/install commands are provided
- [ ] Run command is clear and simple
- [ ] Test command options include file-scoped variants
- [ ] Format/lint commands are documented
- [ ] All commands include flags and arguments needed

### ✅ Troubleshooting
- [ ] Common issues are listed
- [ ] Each issue has a clear solution or skill reference
- [ ] Solutions are actionable (not vague)
- [ ] Covers most frequent developer pain points

### ✅ Maintenance
- [ ] No time-sensitive information (version numbers without context)
- [ ] No references to deprecated tools/patterns
- [ ] All information is current and accurate
- [ ] File has been tested by asking AI agent to perform tasks

## Quick Validation Script

Run these checks programmatically:

```bash
# Check file size (should be under 150 lines)
wc -l AGENTS.md

# Verify no secrets are present
git secrets --scan AGENTS.md  # or
grep -E "(password|api[_-]?key|secret|token)" AGENTS.md

# Check all documentation links exist
grep -oP '`\K[^`]+\.md' AGENTS.md | while read file; do
  [ -f "$file" ] || echo "Missing: $file"
done

# List all skill references
grep -oP '/\K[a-z-]+' AGENTS.md | sort -u

# Verify skill files exist
grep -oP '/\K[a-z-]+' AGENTS.md | while read skill; do
  [ -f ".claude/skills/$skill/SKILL.md" ] || echo "Missing skill: $skill"
done
```

## Pre-Commit Checklist

Before committing AGENTS.md changes:

- [ ] Run `wc -l AGENTS.md` → Should be < 150 lines
- [ ] Test with AI agent: "Set up this project"
- [ ] Test with AI agent: "Add a new feature"
- [ ] Verify all links with script above
- [ ] Ensure no sensitive data with `git secrets --scan`
- [ ] Review diff for accidental deletions or mistakes

## Common Failures

### ❌ File Too Large
**Problem**: AGENTS.md is 300+ lines
**Solution**: Split into subdirectory files or link to detailed guides

### ❌ Broken Links
**Problem**: Documentation Index references files that don't exist
**Solution**: Fix paths or create missing documentation

### ❌ Missing Skills
**Problem**: References `/frontend__feature_builder` but skill doesn't exist
**Solution**: Create skill with `/meta__create_skill` or remove reference

### ❌ Vague Commands
**Problem**: "Run tests" without specific command
**Solution**: Provide exact command: `dotnet test tests/ProjectName/`

### ❌ Duplicated Content
**Problem**: 200 lines explaining event sourcing
**Solution**: Replace with link: `docs/guides/event-sourcing-guide.md`

### ❌ First-Person Voice
**Problem**: "I will help you..." or "You should use..."
**Solution**: "Stack uses..." or "Tests run with..."

## Automation Opportunities

Consider creating pre-commit hooks:

```bash
# .git/hooks/pre-commit
#!/bin/bash
if git diff --cached --name-only | grep -q "AGENTS.md"; then
  echo "Validating AGENTS.md..."

  # Check file size
  lines=$(wc -l < AGENTS.md)
  if [ "$lines" -gt 150 ]; then
    echo "❌ AGENTS.md exceeds 150 lines ($lines)"
    exit 1
  fi

  # Check for secrets
  if git secrets --scan AGENTS.md 2>/dev/null; then
    echo "❌ AGENTS.md contains potential secrets"
    exit 1
  fi

  echo "✅ AGENTS.md validation passed"
fi
```

## Review Questions

Ask yourself:

1. Can an AI agent set up the project using only this file?
2. Can an AI agent add a new feature without asking for clarification?
3. Are all complex workflows delegated to skills?
4. Is every command copy-pasteable and executable?
5. Would a new team member understand the project structure?
6. Are common mistakes clearly documented with solutions?
7. Is sensitive information completely absent?
8. Is the file concise enough to fit in a context window efficiently?

If you answered "No" to any question, revise accordingly.
