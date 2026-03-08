# Contributing to Whispr

1. **Fork and clone** the repo, then open `src/Whispr.sln` in your editor.

2. **Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

3. **Build and test:**
   ```bash
   cd src
   dotnet build Whispr.sln
   dotnet run --project Whispr.Client   # or Whispr.Server
   ```
   Run the test projects in `src/` as needed.

   **Update from git:** After pulling changes, use `./scripts/update-client.sh` to rebuild the client, or `./scripts/update-server.sh --push` to build and push a new server image. See [Server setup → Updating](SERVER_SETUP.md#updating).

4. **Changes:** Use a branch, keep commits focused, and open a **pull request** against `main` with a short description of the change.

5. **Issues:** Bug reports and feature ideas are welcome as [GitHub Issues](/issues).
