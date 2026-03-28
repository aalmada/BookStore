// Go — Install and Example

### Installation

```sh
go get github.com/github/copilot-sdk-go
```

# Checking if Copilot CLI is installed

Before calling the Copilot CLI from Go, you can check if it is available in the system path:

```go
import (
	"log"
	"os/exec"
)

func main() {
	if _, err := exec.LookPath("copilot"); err != nil {
		log.Fatal("Copilot CLI is not installed or not in PATH")
	}
	// ...existing code...
}
```

# Go Example: Basic Usage

```go
package main

import (
	"fmt"
	copilot "github.com/github/copilot-sdk/go"
)

func main() {
	client := copilot.NewClient(nil)
	client.Start()
	defer client.Stop()
	session, _ := client.CreateSession(&copilot.SessionConfig{Model: "gpt-4.1"})
	response, _ := session.SendAndWait(copilot.MessageOptions{Prompt: "Hello"}, 0)
	fmt.Println(*response.Data.Content)
}
```
