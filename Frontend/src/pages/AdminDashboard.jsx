import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import { formatCurrencyVN } from '../utils/format';
import ConfirmModal from '../components/ConfirmModal';

const emptyCoupon = { id:null, code:'', discountType:'Percentage', discountValue:10, expiryDate:'', isActive:true };

export default function AdminDashboard() {
  const [tab, setTab] = useState('overview');
  const [stats, setStats] = useState({});
  const [courses, setCourses] = useState({items:[],page:1,totalPages:0,totalItems:0});
  const [users, setUsers] = useState({items:[],page:1,totalPages:0,totalItems:0});
  const [coupons, setCoupons] = useState({items:[],page:1,totalPages:0,totalItems:0});
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [coupon, setCoupon] = useState(emptyCoupon);
  const [confirmation, setConfirmation] = useState(null);
  const [confirming, setConfirming] = useState(false);

  const loadOverview = async () => { const {data}=await api.get('/admin/stats'); setStats(data||{}); };
  const loadList = async () => {
    setLoading(true); setError('');
    try {
      if (tab === 'courses') { const {data}=await api.get('/admin/courses',{params:{search,page,pageSize:20}}); setCourses(data); }
      if (tab === 'users') { const {data}=await api.get('/admin/users',{params:{search,page,pageSize:20}}); setUsers(data); }
      if (tab === 'coupons') { const {data}=await api.get('/admin/coupons',{params:{search,page,pageSize:20}}); setCoupons(data); }
    } catch(e) { setError(e.response?.data?.message || 'Không thể tải dữ liệu.'); }
    finally { setLoading(false); }
  };
  useEffect(()=>{ loadOverview().catch(()=>setError('Không thể tải overview.')); },[]);
  useEffect(()=>{ setSearch(''); setPage(1); },[tab]);
  useEffect(()=>{ if (!['courses','users','coupons'].includes(tab)) return; const timer=setTimeout(loadList,300); return()=>clearTimeout(timer); },[tab,search,page]);

  const updateCourse = (id,status) => setConfirmation({title:'Cập nhật trạng thái khóa học?',message:`Khóa học sẽ được chuyển sang ${status}.`,confirmLabel:'Cập nhật',action:async()=>{await api.put(`/admin/courses/${id}/status`,JSON.stringify(status),{headers:{'Content-Type':'application/json'}});toast.success('Đã cập nhật khóa học.');await loadList();}});
  const toggleUser = (id,isActive) => setConfirmation({title:isActive?'Khóa tài khoản?':'Mở khóa tài khoản?',message:isActive?'Người dùng sẽ không thể tiếp tục sử dụng tài khoản.':'Người dùng sẽ có thể đăng nhập và sử dụng hệ thống.',confirmLabel:isActive?'Khóa tài khoản':'Mở khóa',danger:isActive,action:async()=>{await api.put(`/admin/users/${id}/toggle-status`);toast.success('Đã cập nhật tài khoản.');await loadList();}});
  const deleteCourse = course => setConfirmation({title:'Xóa khóa học?',message:`“${course.title}” sẽ bị ẩn khỏi tìm kiếm, giỏ hàng và trang công khai. Lịch sử mua và học vẫn được giữ.`,confirmLabel:'Xóa khóa học',danger:true,action:async()=>{await api.delete(`/courses/${course.courseId}`);toast.success('Đã xóa khóa học.');await Promise.all([loadList(),loadOverview()]);}});
  const deleteUser = account => setConfirmation({title:'Xóa tài khoản?',message:`Tài khoản ${account.email} sẽ bị vô hiệu hóa, ẩn danh và thu hồi mọi phiên đăng nhập.`,confirmLabel:'Xóa tài khoản',danger:true,action:async()=>{await api.delete(`/admin/users/${account.userId}`);toast.success('Đã xóa tài khoản.');await Promise.all([loadList(),loadOverview()]);}});
  const confirmAction = async () => { setConfirming(true); try { await confirmation.action(); setConfirmation(null); } catch(e){toast.error(e.response?.data?.message||'Cập nhật thất bại.');} finally {setConfirming(false);} };
  const updateRole = async (id,role) => { try { await api.put(`/admin/users/${id}/role`,JSON.stringify(role),{headers:{'Content-Type':'application/json'}}); loadList(); } catch(e){toast.error(e.response?.data?.message||'Cập nhật role thất bại.');} };
  const saveCoupon = async e => { e.preventDefault(); try { const payload={...coupon,discountValue:Number(coupon.discountValue),expiryDate:new Date(coupon.expiryDate).toISOString()}; if(coupon.id) await api.put(`/admin/coupons/${coupon.id}`,payload); else await api.post('/admin/coupons',payload); toast.success('Đã lưu coupon.'); setCoupon(emptyCoupon); loadList(); } catch(err){toast.error(err.response?.data?.message||'Không thể lưu coupon.');} };
  const generateCode = async () => { try { const {data}=await api.post('/admin/coupons/generate-code'); setCoupon(v=>({...v,code:data.code})); } catch { toast.error('Không thể tạo mã.'); } };
  const toggleCoupon = async id => { try { await api.put(`/admin/coupons/${id}/status`); loadList(); } catch { toast.error('Không thể đổi trạng thái.'); } };
  const pagination = data => <div className="d-flex justify-content-between align-items-center mt-3"><small>{data.totalItems||0} bản ghi</small><div><button className="btn btn-sm btn-outline-primary me-2" disabled={page<=1} onClick={()=>setPage(p=>p-1)}>Trước</button><span>{page}/{data.totalPages||1}</span><button className="btn btn-sm btn-outline-primary ms-2" disabled={page>=(data.totalPages||1)} onClick={()=>setPage(p=>p+1)}>Sau</button></div></div>;

  return <div className="container py-4">
    <div className="d-flex flex-wrap justify-content-between gap-3 border-bottom pb-3 mb-4"><h2 className="text-primary">EduMy Control Center</h2><div className="btn-group flex-wrap">{[['overview','Overview'],['courses','Courses'],['users','Users'],['coupons','Coupons']].map(([key,label])=><button key={key} className={`btn ${tab===key?'btn-primary':'btn-outline-primary'}`} onClick={()=>setTab(key)}>{label}</button>)}</div></div>
    {error && <div className="alert alert-danger">{error}</div>}{loading && <div className="text-center py-3">Đang tải...</div>}

    {tab==='overview' && <><div className="row g-3 mb-4">{[['Users',stats.totalUsers],['Courses',stats.totalCourses],['Students',stats.totalStudents]].map(([label,value])=><div className="col-md-4" key={label}><div className="card p-3 text-center"><small>{label}</small><strong className="fs-4">{value||0}</strong></div></div>)}</div>
      <h4>Khóa học bán gần đây</h4><div className="table-responsive"><table className="table"><thead><tr><th>Khóa học</th><th>Giảng viên</th><th>Người mua</th><th>Giá bán</th><th>Thời gian</th></tr></thead><tbody>{(stats.recentSales||[]).map(s=><tr key={`${s.orderId}-${s.courseId}`}><td><Link to={`/courses/${s.courseId}`}>{s.courseTitle}</Link></td><td>{s.instructorName}</td><td>{s.buyerName}</td><td>{formatCurrencyVN(s.soldPrice)}</td><td>{new Date(s.soldAt).toLocaleString('vi-VN')}</td></tr>)}</tbody></table></div></>}

    {tab==='courses' && <><input className="form-control mb-3" value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}} placeholder="Tìm theo tên khóa học hoặc giảng viên"/><div className="table-responsive"><table className="table align-middle"><thead><tr><th>Khóa học</th><th>Giảng viên</th><th>Trạng thái</th><th>Giá</th><th></th></tr></thead><tbody>{courses.items?.length ? courses.items.map(c=><tr key={c.courseId}><td><Link to={`/courses/${c.courseId}`}>{c.title}</Link></td><td>{c.instructor?.fullName}</td><td>{c.status}</td><td>{formatCurrencyVN(c.price)}</td><td><div className="d-flex gap-2"><button className="btn btn-sm btn-outline-primary" onClick={()=>updateCourse(c.courseId,c.status==='Published'?'Draft':'Published')}>{c.status==='Published'?'Unpublic':'Publish'}</button><button className="btn btn-sm btn-outline-danger" onClick={()=>deleteCourse(c)}>Xóa</button></div></td></tr>):<tr><td colSpan="5" className="text-center">Không có khóa học.</td></tr>}</tbody></table></div>{pagination(courses)}</>}

    {tab==='users' && <><input className="form-control mb-3" value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}} placeholder="Tìm tên, email hoặc role"/><div className="table-responsive"><table className="table align-middle"><thead><tr><th>Người dùng</th><th>Email</th><th>Role</th><th>Trạng thái</th><th></th></tr></thead><tbody>{users.items?.length ? users.items.map(u=><tr key={u.userId}><td><Link to={`/users/${u.userId}`}>{u.fullName}</Link></td><td>{u.email}</td><td><select className="form-select form-select-sm" value={u.role} onChange={e=>updateRole(u.userId,e.target.value)}><option>Student</option><option>Instructor</option><option>Admin</option></select></td><td>{u.isActive?'Active':'Blocked'}</td><td><div className="d-flex gap-2"><button className={`btn btn-sm ${u.isActive?'btn-danger':'btn-success'}`} onClick={()=>toggleUser(u.userId,u.isActive)}>{u.isActive?'Unactive':'Active'}</button><button className="btn btn-sm btn-outline-danger" onClick={()=>deleteUser(u)}>Xóa</button></div></td></tr>):<tr><td colSpan="5" className="text-center">Không có người dùng.</td></tr>}</tbody></table></div>{pagination(users)}</>}

    {tab==='coupons' && <><form className="card p-3 mb-4" onSubmit={saveCoupon}><h4>{coupon.id?'Sửa coupon':'Tạo coupon'}</h4><div className="row g-2"><div className="col-md-3"><input required className="form-control" placeholder="CODE" value={coupon.code} onChange={e=>setCoupon(v=>({...v,code:e.target.value.toUpperCase()}))}/></div><div className="col-md-2"><select className="form-select" value={coupon.discountType} onChange={e=>setCoupon(v=>({...v,discountType:e.target.value}))}><option>Percentage</option><option>FixedAmount</option></select></div><div className="col-md-2"><input required min="0.01" step="0.01" type="number" className="form-control" value={coupon.discountValue} onChange={e=>setCoupon(v=>({...v,discountValue:e.target.value}))}/></div><div className="col-md-3"><input required type="datetime-local" className="form-control" value={coupon.expiryDate} onChange={e=>setCoupon(v=>({...v,expiryDate:e.target.value}))}/></div><div className="col-md-2 d-flex gap-1"><button className="btn btn-primary">Lưu</button><button type="button" className="btn btn-outline-secondary" onClick={generateCode}>Tạo mã tự động</button></div></div></form>
      <input className="form-control mb-3" value={search} onChange={e=>setSearch(e.target.value)} placeholder="Tìm coupon"/><div className="table-responsive"><table className="table"><thead><tr><th>Mã</th><th>Loại</th><th>Giá trị</th><th>Hết hạn</th><th>Trạng thái</th><th></th></tr></thead><tbody>{coupons.items?.map(c=><tr key={c.id}><td>{c.code}</td><td>{c.discountType}</td><td>{c.discountType==='Percentage'?`${c.discountValue}%`:formatCurrencyVN(c.discountValue)}</td><td>{new Date(c.expiryDate).toLocaleString('vi-VN')}</td><td>{c.isActive?'Active':'Inactive'}</td><td><button className="btn btn-sm btn-outline-primary me-1" onClick={()=>setCoupon({...c,expiryDate:new Date(c.expiryDate).toISOString().slice(0,16)})}>Sửa</button><button className="btn btn-sm btn-outline-secondary" onClick={()=>toggleCoupon(c.id)}>Bật/Tắt</button></td></tr>)}</tbody></table></div>{pagination(coupons)}</>}

    <ConfirmModal open={Boolean(confirmation)} title={confirmation?.title} message={confirmation?.message} confirmLabel={confirmation?.confirmLabel} danger={confirmation?.danger} loading={confirming} onCancel={()=>!confirming&&setConfirmation(null)} onConfirm={confirmAction}/>
  </div>;
}
