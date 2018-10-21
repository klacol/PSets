[![Build status](https://ci.appveyor.com/api/projects/status/bg4xece75xits9yq/branch/master?svg=true)](https://ci.appveyor.com/project/klacol/psets/branch/master)


# PSets

Just for testing, Do not use. This is an repo, where I test, how the PSets could be presentend and maintainted with Git/GitHub. Nothing official.

# Schema Specifications
Official releases of the PSets are listed here. PSets releases add semantics to the [IFC schema](https://github.com/buildingSMART/IFC).

Linked released indicate those that are officially released and actively supported.

Release	| Date |	Identifier  |	Documentation	 | ISO Standard |	Release Notes |	Summary
--------|------|--------------|----------------|--------------|---------|----------
4.0.0.5	| 2013 | IFC4	IFC 4.0 | ISO 16739:2013 |	||	Platform improvements with NURBS geometry.
2.3.0.1	| 2007 | IFC2X3_FINAL | IFC 2x3 TC1	   |   |[Release Notes](https://github.com/klacol/PSets/releases/tag/2.3.0.1)|		Documentation expansion and fixes.
2.3.0.0	| 2006 | IFC2X3_FINAL	| IFC 2x3	       |||	Extensions for presentation styling.

# Version Notation
IFC versions are identified using the sematic versioning notation "Major.Minor.Addendum.Corrigendum".

- Major versions consist of scope expansions or deletions and may have changes that break compatibility.
- Minor versions consist of feature extensions, where compatibility is guaranteed for the "core" schema, but not for other definitions.
- Addendums consist of improvements to existing features, where the schema may change but upward compatibility is guaranteed.
- Corrigendums consist of improvements to documentation, where the schema does not change though deprecation is possible.

# Which version do I use?
The latest version, IFC 4.1 is recommended for all current developments, which is fully backward compatible with IFC 4.0. Core definitions within IFC 4.1 and 4.0 are backward compatible with IFC 2.3.
