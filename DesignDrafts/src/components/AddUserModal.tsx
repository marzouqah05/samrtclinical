import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { X, UserPlus, Shield, Building, Mail } from 'lucide-react';

export const AddUserModal: React.FC = () => {
  const { isUserModalOpen, setIsUserModalOpen, addUser, lang } = useApp();
  const isAr = lang === 'ar';

  const [name, setName] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('طبيب');
  const [department, setDepartment] = useState('أمراض القلب');

  if (!isUserModalOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !email.trim()) return;

    const roleEn =
      role === 'مسؤول متفوق'
        ? 'Super Admin'
        : role === 'طبيب'
        ? 'Doctor'
        : role === 'طبيب استشاري'
        ? 'Consultant Doctor'
        : 'Nurse';

    const departmentEn =
      department === 'أمراض القلب'
        ? 'Cardiology'
        : department === 'طب الأطفال'
        ? 'Pediatrics'
        : department === 'طب الأسرة'
        ? 'Family Medicine'
        : 'Management';

    addUser({
      name,
      nameEn: nameEn || name,
      email,
      role,
      roleEn,
      department,
      departmentEn,
      status: 'active',
    });

    setIsUserModalOpen(false);
    setName('');
    setNameEn('');
    setEmail('');
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in">
      <div className="bg-white rounded-xl border border-[#CBD5E1] max-w-md w-full p-6 shadow-2xl relative">
        <div className="flex justify-between items-center pb-3 border-b border-[#E2E8F0] mb-4">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded bg-[#eaedff] text-[#006194] flex items-center justify-center">
              <UserPlus className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-[17px] font-bold text-[#0F172A]">
                {isAr ? 'إضافة مستخدم جديد للنظام' : 'Add New Staff Member'}
              </h3>
              <p className="text-[11px] text-[#64748B]">
                {isAr ? 'منح صلاحيات الوصول للكوادر الطبية والإدارية' : 'Grant role and access permissions'}
              </p>
            </div>
          </div>
          <button
            onClick={() => setIsUserModalOpen(false)}
            className="text-[#64748B] hover:text-[#0F172A] p-1 rounded-md cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-3.5">
          <div>
            <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
              {isAr ? 'الاسم بالكامل (عربي)' : 'Full Name (Arabic)'}
            </label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={isAr ? 'د. أحمد خالد' : 'Dr. Ahmed Khaled'}
              className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[13px] text-[#0F172A]"
            />
          </div>

          <div>
            <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
              {isAr ? 'الاسم بالإنجليزية (English Name)' : 'Full Name (English)'}
            </label>
            <input
              type="text"
              value={nameEn}
              onChange={(e) => setNameEn(e.target.value)}
              placeholder="Dr. Ahmed Khaled"
              dir="ltr"
              className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[13px] text-[#0F172A]"
            />
          </div>

          <div>
            <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
              {isAr ? 'البريد الإلكتروني المهني' : 'Professional Email'}
            </label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="dr.ahmed@clinicflow.com"
              dir="ltr"
              className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[13px] text-[#0F172A]"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
                {isAr ? 'الدور' : 'Role'}
              </label>
              <select
                value={role}
                onChange={(e) => setRole(e.target.value)}
                className="w-full bg-white border border-[#CBD5E1] rounded px-2.5 py-2 text-[13px] text-[#0F172A]"
              >
                <option value="طبيب">{isAr ? 'طبيب' : 'Doctor'}</option>
                <option value="طبيب استشاري">{isAr ? 'طبيب استشاري' : 'Consultant'}</option>
                <option value="ممرض">{isAr ? 'ممرض' : 'Nurse'}</option>
                <option value="مسؤول متفوق">{isAr ? 'مسؤول نظام' : 'Super Admin'}</option>
              </select>
            </div>

            <div>
              <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
                {isAr ? 'القسم' : 'Department'}
              </label>
              <select
                value={department}
                onChange={(e) => setDepartment(e.target.value)}
                className="w-full bg-white border border-[#CBD5E1] rounded px-2.5 py-2 text-[13px] text-[#0F172A]"
              >
                <option value="أمراض القلب">{isAr ? 'أمراض القلب' : 'Cardiology'}</option>
                <option value="طب الأطفال">{isAr ? 'طب الأطفال' : 'Pediatrics'}</option>
                <option value="طب الأسرة">{isAr ? 'طب الأسرة' : 'Family Med'}</option>
                <option value="الإدارة">{isAr ? 'الإدارة' : 'Management'}</option>
              </select>
            </div>
          </div>

          <div className="pt-3 flex justify-end gap-2 border-t border-[#E2E8F0]">
            <button
              type="button"
              onClick={() => setIsUserModalOpen(false)}
              className="px-4 py-2 text-[13px] text-[#64748B] hover:text-[#0F172A] font-medium rounded cursor-pointer"
            >
              {isAr ? 'إلغاء' : 'Cancel'}
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-[#006194] hover:bg-[#004b73] text-white text-[13px] font-medium rounded transition-all cursor-pointer shadow-xs"
            >
              {isAr ? 'تسجيل المستخدم' : 'Save Staff Member'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
