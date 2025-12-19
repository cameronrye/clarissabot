# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.1.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in ClarissaBot, please report it responsibly.

### How to Report

1. **Do NOT open a public issue** for security vulnerabilities
2. Email the maintainer directly at the email listed in the repository
3. Include as much detail as possible:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### What to Expect

- **Acknowledgment**: You will receive a response within 48 hours
- **Updates**: We will keep you informed of our progress
- **Resolution**: We aim to resolve critical issues within 7 days
- **Credit**: We will credit you in the release notes (unless you prefer anonymity)

## Security Best Practices

When deploying ClarissaBot:

1. **Never commit secrets** - Use environment variables or Azure Key Vault
2. **Use managed identity** - The app uses `DefaultAzureCredential` for Azure OpenAI authentication
3. **Keep dependencies updated** - Dependabot is configured to monitor for updates
4. **Review CORS settings** - Ensure API CORS is configured for your domain only
5. **Use HTTPS** - Always deploy with TLS enabled

## Dependencies

This project uses Dependabot to automatically monitor and update dependencies for security patches. Check the `.github/dependabot.yml` configuration for details.

