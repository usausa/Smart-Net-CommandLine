# Diagnostics

| ID | Severity | Description | How to fix |
|---|---|---|---|
| SCL0001 | ❌ Error | Option property does not have a public setter that is not init-only (get-only, init-only or non-public setter), so it is not bound | Give the property a public `set` accessor |
| SCL0002 | ❌ Error | Command type is file-local or nested in a `private` / `protected` type, so the generated registration cannot refer to it; it is not registered | Make the type visible in the assembly |
| SCL0003 | ❌ Error | `DefaultValue` cannot be converted to the property type, so it is not used | Give a value that converts to the property type |
| SCL0004 | ⚠️ Warning | The `EnableSmartCommandLineHostingGenerator` MSBuild property value cannot be parsed, so the default (enabled) is used | Set `true` or `false` |
