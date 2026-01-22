---
name: scaffold-skill
description: Guide for creating and registering a new agent skill.
license: MIT
---

Follow this guide to add a new capability (skill/workflow) to the agent.

## Best Practices

### Frontmatter Requirements
- **name**: Lowercase with hyphens, must match directory name (e.g., `scaffold-write`, not `Scaffold Write`)
- **description**: Clear, concise description explaining when to use this skill
- **license**: MIT (standard for this project)

### Content Structure Guidelines
1. **Opening Statement**: Brief instruction on what this skill accomplishes
2. **Numbered Steps**: Clear, actionable steps with sub-bullets for details
3. **Code Examples**: Use fenced code blocks with language hints
4. **Templates**: Reference template files when available (e.g., `templates/Handler.cs`)
5. **// turbo Comments**: Mark automated steps that can run without user confirmation
6. **Related Skills Section**: Document skill dependencies and relationships

### Related Skills Section Template
Every skill should include a `## Related Skills` section with:

```markdown
## Related Skills

**Prerequisites**:
- `/skill-name` - What must exist/run first

**Next Steps**:
- `/skill-name` - What to run after completion

**Related** (or **Debugging**, **Alternatives**, **Recovery**):
- `/skill-name` - Related skills for specific scenarios

**See Also**:
- Links to related documentation or AGENTS.md files
```

### Quality Checklist
- ✅ Clear, actionable steps numbered sequentially
- ✅ Code examples for complex operations
- ✅ Template file references where applicable
- ✅ Related Skills section with prerequisites and next steps
- ✅ Troubleshooting tips for common issues (where relevant)
- ✅ // turbo markers for automated steps

## Steps to Create a Skill

1. **Plan**
   - **Slug**: Choose a kebab-case name (e.g., `scaffold-feature`)
   - **Goal**: Define what the skill achieves and the steps required

2. **Create Skill File**
   - Create directory `.claude/skills/{slug}/`
   - Create file `.claude/skills/{slug}/SKILL.md`
   - **Content Template**:
     ```markdown
     ---
     name: {slug}
     description: {Short description of what this skill does. Include when to use it.}
     license: MIT
     ---

     Follow this guide to {achieve goal} using strict project standards.

     1. **Step 1**
        - Details...
        - **Template**: `templates/Example.cs` (if applicable)

     2. **Step 2**
        - Details...

     // turbo
     3. **Automated Step (Optional)**
        - Commands to run automatically...

     ## Related Skills

     **Prerequisites**:
     - `/prerequisite-skill` - Must exist before this

     **Next Steps**:
     - `/next-skill` - Run after completion
     - `/verify-feature` - Complete verification

     **See Also**:
     - Relevant AGENTS.md files
     - Documentation links
     ```

3. **Create Templates (Optional)**
   - If the skill generates code, create template files in `.claude/skills/{slug}/templates/`
   - Use placeholders like `{Resource}`, `{Event}`, etc.

4. **Register in AGENTS.md**
   - Add the skill to the root `AGENTS.md` file under `## Agent Skills`
   - Include the `/skill-slug` command format

5. **Verify**
   - Use `read_file` to check `.claude/skills/{slug}/SKILL.md` matches expectations
   - Ensure all links to related skills and documentation are valid

## Related Skills

**Prerequisites**:
- None - this is a foundational skill

**Next Steps**:
- Test the skill by invoking it with `/skill-slug`
- `/verify-feature` - Ensure no broken links or syntax errors

**See Also**:
- Root [AGENTS.md](../../../AGENTS.md) - Agent Skills section
- [agent-guide](../../../docs/guides/agent-guide.md) - Agent development guide
- Other skills in `.claude/skills/` for examples
