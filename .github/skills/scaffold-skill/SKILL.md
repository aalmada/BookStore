---
name: Scaffold Skill
description: Guide for creating and registering a new agent skill.
---

Follow this guide to add a new capability (skill/workflow) to the agent.

1. **Plan**
   - **Slug**: Choose a kebab-case name (e.g., `scaffold-feature`).
   - **Goal**: Define what the skill achieves and the steps required.

2. **Create Skill File**
   - Create directory `.github/skills/{slug}/`.
   - Create file `.github/skills/{slug}/SKILL.md`.
   - **Content Template**:
     ```markdown
     ---
     name: {Human Readable Name}
     description: {Short description of what this skill does}
     ---

     1. **Step 1**
        - Details...

     2. **Step 2**
        - Details...

     // turbo
     3. **Automated Step (Optional)**
        - Commands to run automatically...
     ```

3. **Register (Symlink)**
   - Create a symlink to make it available to the agent:
     ```bash
     ln -sf ../../.github/skills/{slug}/SKILL.md .agent/workflows/{slug}.md
     ```

4. **Update Task List**
   - Add the new skill to the list of completed tasks in `task.md` if applicable.

5. **Verify**
   - Use `view_file` to check `.agent/workflows/{slug}.md` ensures it resolves correctly.
