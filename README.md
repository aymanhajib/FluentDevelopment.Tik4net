# FluentDevelopment.Tik4net 🚀

[![NuGet Version](https://img.shields.io/badge/version-10.0.0-blue)](https://www.nuget.org/)
[![Framework](https://img.shields.io/badge/framework-.NET%2010.0-green)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-important)](LICENSE)

**FluentDevelopment.Tik4net** is a high-performance, enterprise-grade .NET 10 wrapper for the `tik4net` library. It simplifies MikroTik RouterOS API interactions by providing a thread-safe, fluent, and asynchronous interface designed for modern software architecture.

## ✨ Key Features

*   **Smart Connection Pooling:** Minimizes authentication overhead by reusing active sessions.
*   **Asynchronous-First:** Full support for `Task`-based operations to keep your UI and backend responsive.
*   **Long-Lived Connections:** Dedicated handling for persistent tasks like traffic monitoring (`torch`) and real-time statistics.
*   **Robust Error Handling:** Built-in exception mapping that translates complex MikroTik traps into human-readable messages.
*   **Fluent Integration:** Seamlessly integrates with `Microsoft.Extensions.Logging` for industrial-strength traceability.

## 📦 Installation

Install the package via NuGet Package Manager:

```bash
dotnet add package FluentDevelopment.Tik4net --version 10.0.0