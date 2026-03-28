// Node.js — Install and Example

### Installation

```sh
npm install @github/copilot-sdk
```

# Checking if Copilot CLI is installed

Before calling the Copilot CLI from Node.js, you can check if it is available in the system path:

```js
const { execSync } = require('child_process');
try {
	execSync('copilot --version', { stdio: 'ignore' });
} catch (e) {
	throw new Error('Copilot CLI is not installed or not in PATH');
}
```

# Node.js Example: Basic Usage

```typescript
import { CopilotClient } from "@github/copilot-sdk";
const client = new CopilotClient();
const session = await client.createSession({ model: "gpt-4.1" });
const response = await session.sendAndWait({ prompt: "Hello" });
console.log(response?.data.content);
await client.stop();
```
