using Core.Librarys.SQLite;
using Core.Models;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Core.Servicers.Instances
{
    public class Categorys : ICategorys
    {
        private List<CategoryModel> _categories;
        private readonly object _locker = new object();
        public Categorys()
        {
            this._categories = new List<CategoryModel>();
        }
     
        public CategoryModel Create(CategoryModel category)
        {
            using (var db = new TaiDbContext())
            {
                db.Categorys.Add(category);
                db.SaveChanges();
                lock (_locker) _categories.Add(category);
                return category;
            }
        }

        public void Delete(CategoryModel category)
        {
            using (var db = new TaiDbContext())
            {
                var item = db.Categorys.Where(m => m.ID == category.ID).FirstOrDefault();
                if (item != null)
                {
                    db.Categorys.Remove(item);
                    db.SaveChanges();
                    lock (_locker) _categories.RemoveAll(existing => existing.ID == category.ID);
                }
            }

        }

        public List<CategoryModel> GetCategories()
        {
            lock (_locker) return new List<CategoryModel>(_categories);
        }

        public CategoryModel GetCategory(int id)
        {
            lock (_locker) return _categories.FirstOrDefault(m => m.ID == id);
        }

        public void Load()
        {
            Debug.WriteLine("加载分类");
            using (var db = new TaiDbContext())
            {
                lock (_locker) this._categories = db.Categorys.ToList();
                Debug.WriteLine("加载分类完成");

            }
        }


        public void Update(CategoryModel category)
        {
            using (var db = new TaiDbContext())
            {
                db.Entry(category).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
            }
        }
    }
}
