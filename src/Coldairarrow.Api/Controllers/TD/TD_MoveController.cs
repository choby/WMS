﻿using Coldairarrow.Business.IT;
using Coldairarrow.Business.PB;
using Coldairarrow.Business.TD;
using Coldairarrow.Entity.TD;
using Coldairarrow.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Coldairarrow.Api.Controllers.TD
{
    [Route("/TD/[controller]/[action]")]
    public class TD_MoveController : BaseApiController
    {
        #region DI

        public TD_MoveController(ITD_MoveBusiness tD_MoveBus,IOperator @op, ITD_MoveDetailBusiness tD_MoveDetailBus, 
            IIT_LocalMaterialBusiness  iT_LocalMaterialBusiness, IIT_LocalDetailBusiness iT_LocalDetailBusiness,
            IServiceProvider svcProvider)
        {
            _tD_MoveBus = tD_MoveBus;
            _Op = @op;
            _tD_MoveDetailBus = tD_MoveDetailBus;
            _iT_LocalMaterialBusiness = iT_LocalMaterialBusiness;
            _iT_LocalDetailBusiness = iT_LocalDetailBusiness;
            _ServiceProvider = svcProvider;
        }

        ITD_MoveBusiness _tD_MoveBus { get; }
        IOperator _Op { get; }
        ITD_MoveDetailBusiness _tD_MoveDetailBus { get; }
        IIT_LocalMaterialBusiness _iT_LocalMaterialBusiness { get; }
        IIT_LocalDetailBusiness _iT_LocalDetailBusiness { get; }
        IServiceProvider _ServiceProvider { get; }

        #endregion

        #region 获取

        [HttpPost]
        public async Task<PageResult<TD_Move>> GetDataList(PageInput<SearchCondition> input)
        {
            return await _tD_MoveBus.GetDataListAsync(input, _Op.Property.DefaultStorageId);
        }

        [HttpPost]
        public async Task<TD_Move> GetTheData(IdInputDTO input)
        {
            return await _tD_MoveBus.GetTheDataAsync(input.id);
        }

        #endregion

        #region 提交

        [HttpPost]
        public async Task SaveData(TD_Move data)
        {
            if (data.Code.IsNullOrEmpty())
            {
                var codeSvc = _ServiceProvider.GetRequiredService<IPB_BarCodeTypeBusiness>();
                data.Code = await codeSvc.Generate("TD_Move");
            }

            if (data.Id.IsNullOrEmpty())
            {
                InitEntity(data);
                data.StorId = _Op.Property.DefaultStorageId;
                data.Status = (int)MoveStatus.待审核;

                await _tD_MoveBus.AddDataAsync(data);
            }
            else
            {
                await _tD_MoveBus.UpdateDataAsync(data);
            }
        }

        [HttpPost]
        public async Task UpdateData(UpdateMoveInfo updateMoveInfo)
        {
            var data = await _tD_MoveBus.GetTheDataAsync(updateMoveInfo.moveId);
            double moveNum = 0;
            double totalAmt = 0;

            foreach (var i in updateMoveInfo.localNums)
            {
                moveNum += i;
            }
            foreach(var a in updateMoveInfo.amounts)
            {
                totalAmt += a;
            }
            data.MoveNum = moveNum.ToString();
            data.TotalAmt = totalAmt.ToString();

            await _tD_MoveBus.UpdateDataAsync(data);
        }

        [HttpPost]
        public async Task DeleteData(List<string> ids)
        {
            await _tD_MoveBus.DeleteDataAsync(ids);
        }


        [HttpPost]
        public async Task ApproveData(string id)
        {
            await _tD_MoveBus.ApproveDataAsync(id, _Op.UserId);
        }

        [HttpPost]
        public async Task RejectData(string id)
        {
            await _tD_MoveBus.RejectDataAsync(id, _Op.UserId);
        }


        [HttpPost]
        public async Task<AjaxResult> ApproveDatas(List<string> ids)
        {

            var moveList = await _tD_MoveBus.GetDataListAsync(ids);
            var changeList = new List<TD_Move>();

            foreach (var i in moveList)
            {
                if (i.Status == (int)MoveStatus.待审核)
                {
                    changeList.Add(i);
                }
            }

            if (changeList.Count <= 0)
            {
                return Error("所选数据没有待审批数据");
            }
            await _tD_MoveBus.ApproveDatasAsync(changeList.Select(s => s.Id).ToList(), _Op.UserId);

            var dataList = new List<BusinessInfo>();
            var moveDetailList = await _tD_MoveDetailBus.GetDataListByMoveIdsAsync(changeList.Select(s => s.Id).ToList());
            foreach(var i in moveDetailList)
            {
                dataList.Add(new BusinessInfo() {
                    StorId = i.StorId,
                    LocalId = i.FromLocalId,
                    TrayId = i.FromTrayId,
                    ZoneId = i.FromZoneId,
                    MaterialId = i.MaterialId,
                    BarCode = i.BarCode,
                    BatchNo = i.BatchNo,
                    Num = i.LocalNum.HasValue ? i.LocalNum.Value : 0,
                    ActionType = (int)ActionTypeEnum.出库,
                    MeasureId = i.PB_Material.MeasureId
                });

                dataList.Add(new BusinessInfo()
                {
                    StorId = i.StorId,
                    LocalId = i.ToLocalId,
                    TrayId = i.ToTrayId,
                    ZoneId = i.ToZoneId,
                    MaterialId = i.MaterialId,
                    BarCode = i.BarCode,
                    BatchNo = i.BatchNo,
                    Num = i.LocalNum.HasValue ? i.LocalNum.Value : 0,
                    ActionType = (int)ActionTypeEnum.入库,
                    MeasureId = i.PB_Material.MeasureId
                });
            }

            await _iT_LocalMaterialBusiness.UpdataDatasByBussiness(dataList);
            await _iT_LocalDetailBusiness.UpdataDatasByBussiness(dataList, _Op.UserId);
            return Success();
        }

        [HttpPost]
        public async Task RejectDatas(List<string> ids)
        {
            await _tD_MoveBus.RejectDatasAsync(ids, _Op.UserId);
        }
        #endregion
    }
}