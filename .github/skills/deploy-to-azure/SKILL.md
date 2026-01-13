---
name: Deploy To Azure
description: Deploy the BookStore application to Azure using Aspire and azd. Use this to ship the application to production.
---

Deploy the application stack to Azure Container Apps using the Azure Developer CLI (`azd`).

1. **Environment Check**
   - Verify the `.azure` directory exists.
   - If missing, run `azd init (with --no-prompt if possible, or ask user)` to initialize the environment.

2. **Authentication**
   - Run `azd auth login` to ensure the session is active.
   - *Tip*: If the user says they are already logged in, you can verify with `azd auth show-status` (if available) or proceed.

3. **Deploy**
   // turbo
   Run `azd up` to provision resources and deploy the code.
   - This command may take several minutes.
   - Be ready to handle prompts if `SafeToAutoRun` is not enabled, though `// turbo` helps here.

4. **Verify Deployment**
   - Upon success, run `azd show` to retrieve the public endpoints.
   - Display the `webfrontend` URL to the user so they can access their deployed app.
