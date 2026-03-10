// Composite projections group multiple projections into ordered stages.
// Stage 1 projections are committed BEFORE stage 2 projections run.
// This enables downstream projections to depend on upstream read model output.
//
// Register in MartenConfigurationExtensions.RegisterProjections():
//
// options.Projections.CompositeProjectionFor("AuthorDetails", group =>
// {
//     // Stage 1 (default) — run in parallel
//     group.Add<AuthorProjection>();
//     group.Add<AuthorStatisticsProjectionBuilder>();
//
//     // Stage 2 — runs after Stage 1 is committed
//     group.Add<AuthorDashboardProjectionBuilder>(2);
// });
//
// Each member projection is defined as a normal POCO snapshot, SingleStreamProjection,
// or MultiStreamProjection in its own file. No special base class is needed here.
//
// See projections.md for complete guidance.
