using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.Contract;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;

namespace NsiTransfer.BLL.UseCases;

public class AdminPanelUseCases : IAdminPanelUseCases
{
    private readonly IPolynomApiService _apiService;

    public AdminPanelUseCases(IPolynomApiService apiService)
    {
        _apiService = apiService;
    }


    public Task<Result<List<ClassificationTreeNode>>> GetReferenceFirstLayerNodes(CancellationToken cancellationToken = default)
    {
        return _apiService.GetClassificationRootNode(cancellationToken)
            .BindAsync(rootNode => _apiService.GetClassificationChildrenNodes(rootNode, cancellationToken));
    }
}
