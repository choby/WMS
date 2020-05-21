﻿using AutoMapper;
using Coldairarrow.Business.PB;
using Coldairarrow.Entity.Base;
using Coldairarrow.Util;
using EFCore.Sharding;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace Coldairarrow.Business.Base
{
    public class Base_UserStorBusiness : BaseBusiness<Base_UserStor>, IBase_UserStorBusiness, ITransientDependency
    {
        readonly Cache.IBase_UserCache _userCache;
        readonly IServiceProvider _serviceProvider;
        readonly IMapper _mapper;
        public Base_UserStorBusiness(IRepository repository, Cache.IBase_UserCache userCache, IServiceProvider serviceProvider,IMapper mapper)
            : base(repository)
        {
            _serviceProvider = serviceProvider;
            _userCache = userCache;
            _mapper = mapper;
        }

        #region 外部接口

        public async Task<PageResult<Base_UserStor>> GetDataListAsync(PageInput<Base_UserStorQM> input)
        {
            var q = GetIQueryable().Include(i => i.User).Include(i => i.Storage);
            var where = LinqHelper.True<Base_UserStor>();
            var search = input.Search;

            if (!search.UserName.IsNullOrEmpty())
                where = where.And(w => w.User.RealName.Contains(search.UserName) || w.User.UserName.Contains(search.UserName));
            if (!search.StorageName.IsNullOrEmpty())
                where = where.And(w => w.Storage.Name.Contains(search.StorageName) || w.Storage.Code.Contains(search.StorageName));

            return await q.Where(where).GetPageResultAsync(input);
        }

        public async Task<Base_UserStor> GetTheDataAsync(string id)
        {
            return await GetEntityAsync(id);
        }

        public async Task AddDataAsync(Base_UserStor data)
        {
            await InsertAsync(data);
        }
        public async Task UpdateDefault(string userId)
        {
            await UpdateWhereAsync(w => w.UserId == userId && w.IsDefault, entity => { entity.IsDefault = false; });
        }
        public async Task UpdateDataAsync(Base_UserStor data)
        {
            await UpdateAsync(data);
        }

        public async Task DeleteDataAsync(List<string> ids)
        {
            await DeleteAsync(ids);
        }
        public async Task<List<PB_StorageDTO>> GetStorage(string userId)
        {
            var storSvc = _serviceProvider.GetRequiredService<PB.IPB_StorageBusiness>();
            var listStor = await storSvc.GetListAsync();
            var storDto = _mapper.Map<List<Entity.PB.PB_Storage>, List<PB_StorageDTO>>(listStor);
            storDto = storDto.OrderByDescending(o => o.IsDefault).ToList();
            var defaultStorId = storDto.SingleOrDefault(w => w.IsDefault)?.Id;//系统默认仓库Id
            var userStors = await this.GetIQueryable().Where(w => w.UserId == userId).OrderByDescending(o => o.IsDefault).ToListAsync();
            //过滤用户有权限的仓库
            if (userStors.Count > 0)
            {
                var userStorIds = userStors.Select(s => s.StorId).ToList();
                storDto = storDto.Where(w => userStorIds.Contains(w.Id)).ToList();
            }
            //设置用户的默认仓库
            var defaultUserStorId = userStors.SingleOrDefault(w => w.IsDefault)?.StorId;//用户默认仓库ID
            var defaultId = defaultUserStorId.IsNullOrEmpty() ? defaultStorId : defaultUserStorId;
            if (!defaultId.IsNullOrEmpty())
                storDto.ForEach(item => { item.IsDefault = item.Id == defaultId; });
            else
            {
                var first = storDto.FirstOrDefault();
                first.IsDefault = true;
            }
            return storDto;
        }
        public async Task<string> GetDefaultStorageId(string userId)
        {
            var listStor = await this.GetStorage(userId);
            return listStor.SingleOrDefault(w => w.IsDefault)?.Id;
        }
        public async Task SwitchDefault(string userId, string storageId)
        {
            await UpdateWhereAsync(w => w.UserId == userId && w.IsDefault, (entity) => { entity.IsDefault = false; });
            var userStorage = await this.GetIQueryable().SingleOrDefaultAsync(w => w.UserId == userId && w.StorId == storageId);
            if (userStorage == null)
            {
                userStorage = new Base_UserStor() { };
                userStorage.Id = IdHelper.GetId();
                userStorage.UserId = userId;
                userStorage.StorId = storageId;
                userStorage.IsDefault = true;
                userStorage.CreateTime = DateTime.Now;
                userStorage.CreatorId = userId;
                userStorage.Deleted = false;
                await InsertAsync(userStorage);
            }
            else
            {
                userStorage.IsDefault = true;
                await UpdateDataAsync(userStorage);
            }
            await _userCache.UpdateCacheAsync(userId);
        }
        #endregion

        #region 私有成员

        #endregion
    }
}