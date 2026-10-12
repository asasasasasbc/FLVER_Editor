# Native Cloth build resources

These are required, reviewable source-distribution inputs, not Java runtime tools:

- `native-schema.xml`: embedded corrected BB layout class closure.
- `native-schema-ds3.xml`: embedded separate DS3 layout class closure.
- `native-schema*-provenance.json`: upstream source and extraction hashes.
- `LICENSE`: retained DexesTTP HKXPack MIT copyright/license.

The local C# port is informed by DexesTTP / JeNoVaViRuS hkxpack-souls.
See the repository README and `cloth-and-animation-README.md` for credits and scope.
No proprietary Havok runtime is included. These are native C# build resources.

Historical Java code, Jars, baseline schemas, one-off extraction/oracle scripts,
game-derived golden XML, logs and screenshots live under the **ignored local**
`helper-ext/` directory at the repository root. They are not part of the GitHub
source distribution and are not build inputs. Do not add them with `git add -f`.
