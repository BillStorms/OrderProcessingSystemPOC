# Security Best Practices

## ⚠️ Important: Never Commit Secrets

This repository uses environment variables to manage sensitive configuration. **DO NOT** commit the `.env` file.

### Setup Instructions

1. Copy the example environment file:
   ```bash
   cp .env.example .env
   ```

2. Update `.env` with your local values:
   ```bash
   # For local development, you can use the example password
   # For any shared or production environment, generate a strong password
   SQL_SA_PASSWORD=<your-secure-password>
   ```

3. Start the services:
   ```bash
   docker compose up
   ```

### Development vs. Production

**Development:**
- The `.env.example` file contains placeholder values safe for local development
- Never use these values in any shared or internet-facing environment

**Production:**
- Use a secrets management system (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- Generate cryptographically strong passwords (minimum 20 characters, mixed case, numbers, symbols)
- Rotate credentials regularly (every 90 days minimum)
- Use managed identities/service principals instead of passwords where possible

### Files That Should NEVER Be Committed

- `.env` - Contains actual secrets
- `*.key` - Private keys
- `*.pem` - Certificate files (private keys)
- `secrets/` - Any directory containing secrets
- `appsettings.Production.json` - If it contains connection strings or API keys

### Current Configuration Files

**Safe to commit:**
- ✅ `.env.example` - Template with placeholder values
- ✅ `appsettings.json` - Base configuration (no secrets)
- ✅ `appsettings.Development.json` - **IF UPDATED** to reference environment variables
- ✅ `docker-compose.yml` - Uses environment variable substitution

**NOT safe to commit (currently):**
- ⚠️ `appsettings.json` - Contains hardcoded password (needs update)
- ⚠️ `appsettings.Development.json` - Contains hardcoded password (needs update)

### Recommended Next Steps

1. **Update appsettings files** to use User Secrets or environment variables:
   ```json
   {
     "ConnectionStrings": {
       "OrderDatabase": "Server=localhost,1433;Database=OrderServiceDb;User Id=sa;Password=PLACEHOLDER;TrustServerCertificate=True;"
     }
   }
   ```
   Add note: "For local development, use User Secrets or .env file"

2. **Use .NET User Secrets** for local development:
   ```bash
   cd order-service/OrderService.Api
   dotnet user-secrets set "ConnectionStrings:OrderDatabase" "Server=localhost,1433;Database=OrderServiceDb;User Id=sa;Password=YourLocalPassword;TrustServerCertificate=True;"
   ```

3. **Clean git history** if secrets were already committed:
   ```bash
   # If you've already committed sensitive data, you MUST clean the history
   # https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository
   ```

### Verification Checklist

Before pushing to a public repository:

- [ ] `.env` is in `.gitignore`
- [ ] `.env.example` contains only placeholder values
- [ ] `docker-compose.yml` uses `${VARIABLE}` syntax for secrets
- [ ] `appsettings.json` files do NOT contain hardcoded passwords
- [ ] No API keys, tokens, or certificates in source code
- [ ] Run `git secrets --scan` or `gitleaks` to detect secrets

---

**Remember:** Once a secret is pushed to a public repository, assume it is compromised. Rotate immediately.
