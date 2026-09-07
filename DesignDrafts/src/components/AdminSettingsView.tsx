import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import {
  UserPlus,
  Cloud,
  CloudCheck,
  Download,
  RefreshCw,
  ShieldCheck,
  MoreVertical,
  CheckCircle2,
  Trash2,
  Edit2,
  HardDrive,
  Lock,
} from 'lucide-react';

export const AdminSettingsView: React.FC = () => {
  const {
    lang,
    users,
    addUser,
    backups,
    isSyncing,
    triggerForceSync,
    downloadSnapshot,
    downloadFullArchive,
    setIsUserModalOpen,
    addToast,
  } = useApp();

  const isAr = lang === 'ar';
  const [selectedQuickFill, setSelectedQuickFill] = useState('');

  const quickFillOptions = [
    {
      name: 'د. سارة سميث',
      nameEn: 'Dr. Sarah Smith',
      email: 'dr.smith@clinicflow.com',
      role: 'طبيب استشاري',
      roleEn: 'Consultant Doctor',
      department: 'أمراض القلب',
      departmentEn: 'Cardiology',
    },
    {
      name: 'د. جيمس جونز',
      nameEn: 'Dr. James Jones',
      email: 'dr.jones@clinicflow.com',
      role: 'طبيب أطفال',
      roleEn: 'Pediatrician',
      department: 'طب الأطفال',
      departmentEn: 'Pediatrics',
    },
    {
      name: 'د. إيميلي لي',
      nameEn: 'Dr. Emily Lee',
      email: 'dr.lee@clinicflow.com',
      role: 'ممارسة عامة',
      roleEn: 'General Practitioner',
      department: 'طب الأسرة',
      departmentEn: 'Family Medicine',
    },
  ];

  const handleQuickFill = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    setSelectedQuickFill(val);
    const found = quickFillOptions.find((o) => o.nameEn === val);
    if (found) {
      // Check if user already exists
      const exists = users.some((u) => u.email === found.email);
      if (!exists) {
        addUser({
          ...found,
          status: 'active',
        });
      } else {
        addToast(
          isAr ? 'المستخدم مسجل بالفعل' : 'User Already Registered',
          found.nameEn,
          'info'
        );
      }
    }
  };

  return (
    <div className="flex-1 overflow-y-auto p-4 md:p-6 lg:p-8 custom-scrollbar">
      <div className="max-w-[1440px] mx-auto flex flex-col gap-6">
        {/* Top Summary Banner */}
        <div className="bg-[#006194]/10 border border-[#006194]/20 rounded-xl p-4 flex flex-col sm:flex-row justify-between items-center gap-3">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-[#006194] text-white flex items-center justify-center">
              <ShieldCheck className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-[16px] font-bold text-[#0F172A]">
                {isAr ? 'مركز الإشراف والتحكم بالمنظومة الطبية' : 'Medical Operations & Security Center'}
              </h3>
              <p className="text-[12px] text-[#64748B]">
                {isAr
                  ? 'إدارة صلاحيات الكوادر الطبية والمزامنة السحابية المشفرة'
                  : 'Manage clinical staff access controls and encrypted cloud backups'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-[#DCFCE7] text-[#166534] text-[12px] font-semibold">
              <span className="w-2 h-2 rounded-full bg-[#166534] animate-pulse" />
              HIPAA & GDPR Compliant
            </span>
          </div>
        </div>

        {/* 8 Cols User Management + 4 Cols Triple-Layer Backup */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* User Management (8 Cols) */}
          <section
            id="user-management-section"
            className="lg:col-span-8 bg-white rounded-xl border border-[#E2E8F0] flex flex-col overflow-hidden shadow-xs"
          >
            {/* Header & Quick Fill */}
            <div className="p-4 border-b border-[#E2E8F0] bg-[#F8FAFC] flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
              <div>
                <h4 className="text-[17px] font-bold text-[#0F172A]">
                  {isAr ? 'إدارة المستخدمين والكوادر الطبية' : 'Staff & User Management'}
                </h4>
                <p className="text-[12px] text-[#64748B]">
                  {users.length} {isAr ? 'مستخدمين نشطين' : 'registered staff members'}
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-2 w-full sm:w-auto">
                {/* Quick Fill Dropdown */}
                <select
                  value={selectedQuickFill}
                  onChange={handleQuickFill}
                  className="bg-white border border-[#CBD5E1] rounded-md px-2.5 py-1.5 text-[12px] text-[#0F172A] font-medium focus:outline-none focus:border-[#006194] cursor-pointer"
                >
                  <option value="">
                    {isAr ? '⚡ تعبئة سريعة للبيانات' : '⚡ Quick Fill Credentials'}
                  </option>
                  {quickFillOptions.map((opt) => (
                    <option key={opt.nameEn} value={opt.nameEn}>
                      {isAr ? opt.name : opt.nameEn} ({isAr ? opt.department : opt.departmentEn})
                    </option>
                  ))}
                </select>

                {/* Add User Button */}
                <button
                  id="btn-add-user"
                  onClick={() => setIsUserModalOpen(true)}
                  className="px-3 py-1.5 bg-[#006194] hover:bg-[#004b73] text-white text-[12px] font-medium rounded-md transition-all flex items-center gap-1.5 cursor-pointer shadow-2xs"
                >
                  <UserPlus className="w-3.5 h-3.5" />
                  <span>{isAr ? 'إضافة مستخدم' : 'Add User'}</span>
                </button>
              </div>
            </div>

            {/* Users Table */}
            <div className="flex-1 overflow-x-auto">
              <table className="w-full text-[13px] text-start border-collapse">
                <thead>
                  <tr className="bg-[#F8FAFC] border-b border-[#E2E8F0] text-[#64748B] text-[11px] uppercase tracking-wider font-semibold">
                    <th className="py-3 px-4 text-start">{isAr ? 'الاسم' : 'Name'}</th>
                    <th className="py-3 px-4 text-start">{isAr ? 'البريد' : 'Email'}</th>
                    <th className="py-3 px-4 text-start">{isAr ? 'الدور' : 'Role'}</th>
                    <th className="py-3 px-4 text-start">{isAr ? 'القسم' : 'Department'}</th>
                    <th className="py-3 px-4 text-start">{isAr ? 'الحالة' : 'Status'}</th>
                    <th className="py-3 px-4 text-end">{isAr ? 'الإجراءات' : 'Actions'}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#E2E8F0]">
                  {users.map((user) => (
                    <tr key={user.id} className="hover:bg-[#F8FAFC] transition-colors group">
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-2.5">
                          <div className="w-8 h-8 rounded-full bg-[#eaedff] text-[#006194] font-bold text-[12px] flex items-center justify-center shrink-0 border border-[#dae2fd]">
                            {user.initials}
                          </div>
                          <span className="font-bold text-[#0F172A]">
                            {isAr ? user.name : user.nameEn}
                          </span>
                        </div>
                      </td>

                      <td className="py-3 px-4 text-[#64748B]" dir="ltr">
                        {user.email}
                      </td>

                      <td className="py-3 px-4">
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold bg-[#eaedff] text-[#006194]">
                          {isAr ? user.role : user.roleEn}
                        </span>
                      </td>

                      <td className="py-3 px-4 text-[#64748B]">
                        {isAr ? user.department : user.departmentEn}
                      </td>

                      <td className="py-3 px-4">
                        <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-[#DCFCE7] text-[#166534]">
                          <span className="w-1.5 h-1.5 rounded-full bg-[#166534]" />
                          {isAr ? 'نشط' : 'Active'}
                        </span>
                      </td>

                      <td className="py-3 px-4 text-end">
                        <div className="flex items-center justify-end gap-1 opacity-70 group-hover:opacity-100">
                          <button
                            onClick={() =>
                              addToast(
                                isAr ? 'تعديل الصلاحيات' : 'Edit Permissions',
                                isAr ? user.name : user.nameEn,
                                'info'
                              )
                            }
                            className="p-1 text-[#64748B] hover:text-[#006194] rounded cursor-pointer"
                          >
                            <Edit2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* Triple-Layer Backup & Google Drive Sync (4 Cols) */}
          <aside
            id="backup-sync-section"
            className="lg:col-span-4 flex flex-col gap-6"
          >
            <div className="bg-white rounded-xl border border-[#E2E8F0] p-5 shadow-xs flex flex-col gap-4">
              <div className="flex justify-between items-start">
                <div>
                  <h4 className="text-[17px] font-bold text-[#0F172A]">
                    {isAr ? 'النسخ الاحتياطي ثلاثي الطبقات' : 'Triple-Layer Backup'}
                  </h4>
                  <p className="text-[12px] text-[#64748B]">
                    {isAr ? 'مزامنة Google Drive والتخزين المشفر' : 'Google Drive Sync & Encrypted Storage'}
                  </p>
                </div>
                <div className="w-8 h-8 rounded bg-[#eaedff] text-[#006194] flex items-center justify-center">
                  <HardDrive className="w-4 h-4" />
                </div>
              </div>

              {/* Live Connection Card */}
              <div className="p-3 bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg">
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span className="material-symbols-outlined text-[#166534] text-[20px]">
                      cloud_done
                    </span>
                    <span className="text-[13px] font-bold text-[#0F172A]">
                      Google Drive
                    </span>
                  </div>
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#DCFCE7] text-[#166534] text-[11px] font-bold">
                    <span className="w-1.5 h-1.5 rounded-full bg-[#166534] animate-ping" />
                    {isAr ? 'متصل' : 'Connected'}
                  </span>
                </div>
                <p className="text-[11px] text-[#64748B]">
                  {isAr ? 'آخر مزامنة: منذ 10 دقائق' : 'Last Sync: 10 mins ago'}
                </p>
              </div>

              {/* Action Buttons */}
              <div className="flex flex-col gap-2">
                {/* Force Sync Button */}
                <button
                  id="btn-force-sync"
                  onClick={triggerForceSync}
                  disabled={isSyncing}
                  className="w-full h-10 bg-[#006194] hover:bg-[#004b73] disabled:opacity-50 text-white text-[13px] font-medium rounded-md transition-all flex items-center justify-center gap-2 cursor-pointer shadow-xs active:scale-98"
                >
                  <RefreshCw className={`w-4 h-4 ${isSyncing ? 'animate-spin' : ''}`} />
                  <span>{isAr ? 'مزامنة الآن (Force Sync)' : 'Force Sync Now'}</span>
                </button>

                {/* Download Snapshot Button */}
                <button
                  id="btn-download-snapshot"
                  onClick={downloadSnapshot}
                  className="w-full h-10 bg-white border border-[#CBD5E1] text-[#0F172A] hover:bg-[#F8FAFC] text-[13px] font-medium rounded-md transition-all flex items-center justify-center gap-2 cursor-pointer"
                >
                  <Download className="w-4 h-4 text-[#006194]" />
                  <span>{isAr ? 'تحميل لقطة اليوم (2.4GB)' : "Download Today's Snapshot"}</span>
                </button>

                {/* Full Archive */}
                <button
                  id="btn-full-archive"
                  onClick={downloadFullArchive}
                  className="w-full py-1.5 text-[12px] text-[#64748B] hover:text-[#006194] font-medium transition-colors"
                >
                  {isAr ? 'طلب أرشيف السجلات الكامل' : 'Request Full Archive'}
                </button>
              </div>

              {/* Recent Backups List */}
              <div className="border-t border-[#E2E8F0] pt-3">
                <h5 className="text-[12px] font-bold text-[#64748B] uppercase tracking-wider mb-2.5">
                  {isAr ? 'النسخ السابقة' : 'Recent Snapshots'}
                </h5>
                <div className="space-y-2">
                  {backups.map((b) => (
                    <div
                      key={b.id}
                      className="flex items-center justify-between p-2 rounded bg-[#F8FAFC] border border-[#E2E8F0] text-[12px]"
                    >
                      <div className="flex items-center gap-2">
                        <CheckCircle2 className="w-3.5 h-3.5 text-[#166534]" />
                        <span className="font-semibold text-[#0F172A]">
                          {isAr ? b.date : b.dateEn}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="text-[#64748B] font-mono text-[11px]" dir="ltr">
                          {b.size}
                        </span>
                        <button
                          onClick={() =>
                            addToast(
                              isAr ? 'تحميل النسخة' : 'Downloading snapshot',
                              b.date,
                              'success'
                            )
                          }
                          className="p-1 text-[#006194] hover:bg-[#eaedff] rounded"
                        >
                          <Download className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Encryption Footer Note */}
              <div className="p-2 bg-[#F1F5F9] rounded text-[11px] text-[#64748B] flex items-center gap-2">
                <Lock className="w-3.5 h-3.5 text-[#006194] shrink-0" />
                <span>
                  {isAr
                    ? 'مشفر بـ AES-256 • تخزين سحابي متوافق مع معايير الأمان الدولية'
                    : 'AES-256 Encrypted Cloud Storage • International compliance'}
                </span>
              </div>
            </div>
          </aside>
        </div>
      </div>
    </div>
  );
};
