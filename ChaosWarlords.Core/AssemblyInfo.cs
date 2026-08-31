using System.Runtime.CompilerServices;

// Several logic-layer types (Player, Site, ...) intentionally expose mutation members as
// `internal` rather than `public` - restricted to trusted callers (Managers) inside this
// assembly, per docs/coding-guidelines.md's encapsulation rules. Splitting the client
// project out into its own assembly (ChaosWarlords.csproj) would otherwise make those
// members invisible there even though the client's own Managers/Rendering code still
// needs them (e.g. Site.Spies read by MapRenderer for spy-count display). Grant both the
// client and test assemblies the same access an in-assembly caller would have, rather
// than loosening these members to public just to cross the new assembly boundary.
[assembly: InternalsVisibleTo("ChaosWarlords")]
[assembly: InternalsVisibleTo("ChaosWarlords.Tests")]
[assembly: InternalsVisibleTo("ChaosWarlords.Core.Tests")]
