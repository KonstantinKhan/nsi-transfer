using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Interfaces.UseCases;

public interface IAdminPanelUseCases
{
    Task<Result<List<ClassificationTreeNode>>> GetReferenceFirstLayerNodes(CancellationToken cancellationToken = default);
}
