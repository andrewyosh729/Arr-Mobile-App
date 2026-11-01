using System.Collections.Generic;

namespace ArrApp.UI.ViewModels;

/// <summary>
/// One line of the virtualized collection. The grid is laid out as rows of N cards (N depends on the screen width) so a
/// plain virtualizing list can render it: only the rows on screen exist as controls, however big the library is.
/// </summary>
public abstract class MediaRowViewModel : ViewModelBase;

/// <summary>A slot in a grid row. The last row is padded with empty slots so its cards keep the same size as the others.</summary>
public sealed class MediaCellViewModel(MediaCardViewModel? card) {
    public MediaCardViewModel? Card { get; } = card;
    public bool HasCard => Card is not null;
}

public sealed class GridRowViewModel(IReadOnlyList<MediaCellViewModel> cells) : MediaRowViewModel {
    public IReadOnlyList<MediaCellViewModel> Cells { get; } = cells;
}

public sealed class ListRowViewModel(MediaCardViewModel card) : MediaRowViewModel {
    public MediaCardViewModel Card { get; } = card;
}
