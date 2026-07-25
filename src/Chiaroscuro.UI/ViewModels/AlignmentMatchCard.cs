namespace Chiaroscuro.UI.ViewModels;

/// <summary>One "Golden Highlight Card" in the inverse solver's results strip - a single
/// alignment match, pre-formatted for display. Kept separate from
/// <see cref="Chiaroscuro.Core.InverseSolver.AlignmentMatch"/> so XAML bindings never need to
/// format NodaTime types directly (matching how <see cref="MainViewModel.ResultText"/> is
/// already a pre-formatted string rather than something bound through converters).</summary>
/// <param name="DateLabel">The match's date, formatted for display (e.g. "Mar 15").</param>
/// <param name="TimeLabel">The match's time, formatted for display (e.g. "2:45 PM").</param>
/// <param name="AngleLabel">How close the match was, formatted for display (e.g. "0.30° off").</param>
/// <param name="DateTime">
/// The match's local wall-clock date and time, unformatted - used by
/// <c>MainViewModel.OnSelectedAlignmentMatchChanged</c> to jump the app's Date/TimeOfDay to
/// this match when the card is clicked, without having to re-parse the display strings.
/// </param>
public sealed record AlignmentMatchCard(string DateLabel, string TimeLabel, string AngleLabel, DateTime DateTime);
