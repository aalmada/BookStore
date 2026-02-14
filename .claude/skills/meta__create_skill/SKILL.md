---
name: meta__create_skill
description: Creates new agent skills with proper structure, templates, and documentation. Use when adding new capabilities or workflows to the agent system.
aliases:
  - /scaffold-skill
---

Follow this guide to add a new capability (skill/workflow) to the agent.

## Quality Standards

### Frontmatter Requirements
- **name**: Maximum 64 characters, lowercase letters/numbers/hyphens only, must match directory name
- **description**: Maximum 1024 characters, must describe both what the skill does and when to use it, third-person voice only ("Processes files..." not "I process files...")

### Content Structure
- **Opening Statement**: Brief instruction on what this skill accomplishes
- **Numbered Steps**: Clear, actionable steps with sub-bullets for details
- **Templates**: Reference template files for code generation (e.g., `templates/Handler.cs`)
- **Token Budget**: Keep SKILL.md body under 500 lines; split larger content into separate files
- **Progressive Disclosure**: Link to detailed files rather than inlining all content
- **// turbo Markers**: Mark automated steps that can run without user confirmation
- **Related Skills Section**: Document skill dependencies and relationships

### Checklist
- ✅ Description includes both "what" and "when to use"
- ✅ Description uses third-person voice
- ✅ SKILL.md body under 500 lines
- ✅ Clear, actionable steps numbered sequentially
- ✅ Template file references for code artifacts
- ✅ Related Skills section with prerequisites and next steps
- ✅ // turbo markers for automated steps
- ✅ No time-sensitive information
- ✅ Consistent terminology throughout

## Steps to Create a Skill

1. **Plan**
   - **Name**: Follow the `prefix__slug` convention (e.g., `marten__list_query`)
   - **Goal**: Define what the skill achieves and the steps required

2. **Create Skill File**
   - Create directory `.claude/skills/{prefix__slug}/`
   - Create file `.claude/skills/{prefix__slug}/SKILL.md` using [templates/SKILL.md](templates/SKILL.md)
   - Replace all `{placeholders}` with your actual content

3. **Create Supporting Resources (If Needed)**
   - If the skill generates code or other artifacts, create `.claude/skills/{prefix__slug}/templates/`
   - **Source Code Templates**: Create `.cs`, `.razor`, or other source files with placeholders
     - Use placeholders: `{ClassName}`, `{Resource}`, `{Property}`, etc.
     - Examples: `Command.cs`, `Handler.cs`, `Endpoint.cs`, `Page.razor`
   - **Configuration Templates**: Create `.json`, `.yaml`, or config files if needed
   - **Documentation**: Add additional `.md` files for complex workflows
     - Keep references one level deep (link directly from SKILL.md)
     - Add table of contents for files over 100 lines
   - **Scripts**: Include shell scripts or PowerShell for automation steps
     - Use forward slashes for paths (Unix-style, works everywhere)

4. **Register in AGENTS.md**
   - Add the skill to the root `AGENTS.md` file under `## Skills`
   - Include the canonical `/prefix__slug` command with a brief description

// turbo
5. **Verify**
   - Check `.claude/skills/{slug}/SKILL.md` follows the template structure
   - Ensure all links to related skills and documentation are valid
   - Confirm template files use consistent placeholder naming

## Related Skills

**Prerequisites**:
- None - this is a foundational skill

**Next Steps**:
- Test the skill by invoking it with `/prefix__slug`

**See Also**:
- Relevant AGENTS.md files
- Other skills in `.claude/skills/` for examples
