<script setup lang="ts">
/** Admin category management: create, rename, re-parent and delete. */
import { onMounted, reactive, ref } from 'vue'
import * as adminApi from '@/api/admin'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import type { AdminCategory, AdminCategoryRequest } from '@/types'

const ui = useUiStore()

const categories = ref<AdminCategory[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const editingId = ref<number | null>(null)

const form = reactive<AdminCategoryRequest>({
  name: '',
  slug: null,
  description: null,
  isActive: true,
  sortOrder: 0,
  parentId: null,
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    categories.value = await adminApi.getAdminCategories()
  } catch (cause) {
    error.value = toErrorMessage(cause, 'Categories could not be loaded.')
  } finally {
    loading.value = false
  }
}

onMounted(load)

function reset(): void {
  Object.assign(form, {
    name: '',
    slug: null,
    description: null,
    isActive: true,
    sortOrder: 0,
    parentId: null,
  })
  editingId.value = null
}

function startEdit(category: AdminCategory): void {
  Object.assign(form, {
    name: category.name,
    slug: category.slug,
    description: category.description,
    isActive: category.isActive,
    sortOrder: category.sortOrder,
    parentId: category.parentId,
  })
  editingId.value = category.id
}

/** True when the chosen parent is the category itself or one of its descendants. */
function wouldCreateCycle(parentId: number | null, categoryId: number): boolean {
  if (parentId === null) return false
  if (parentId === categoryId) return true

  let current = categories.value.find((c) => c.id === parentId)
  let guard = 0

  while (current?.parentId != null && guard < 32) {
    if (current.parentId === categoryId) return true
    current = categories.value.find((c) => c.id === current!.parentId)
    guard++
  }

  return false
}

async function save(): Promise<void> {
  if (!form.name.trim()) {
    ui.notifyError('Name is required.')
    return
  }

  if (editingId.value !== null && wouldCreateCycle(form.parentId ?? null, editingId.value)) {
    ui.notifyError('A category cannot be moved under one of its own children.')
    return
  }

  saving.value = true
  try {
    if (editingId.value !== null) {
      await adminApi.updateCategory(editingId.value, { ...form })
      ui.notifySuccess('Category updated.')
    } else {
      await adminApi.createCategory({ ...form })
      ui.notifySuccess('Category created.')
    }
    reset()
    await load()
  } catch (cause) {
    ui.notifyError(toErrorMessage(cause, 'The category could not be saved.'))
  } finally {
    saving.value = false
  }
}

async function remove(category: AdminCategory): Promise<void> {
  try {
    await adminApi.deleteCategory(category.id)
    ui.notifySuccess('Category deleted.')
    if (editingId.value === category.id) reset()
    await load()
  } catch (cause) {
    ui.notifyError(toErrorMessage(cause, 'The category could not be deleted.'))
  }
}
</script>

<template>
  <div>
    <h2 class="h5 mb-3">Categories</h2>

    <form class="summary-card mb-4" style="position: static" novalidate @submit.prevent="save">
      <h3 class="h6 mb-3">{{ editingId !== null ? 'Edit category' : 'New category' }}</h3>

      <div class="row g-3">
        <div class="col-md-4">
          <div class="field">
            <label for="cat-name">Name</label>
            <input id="cat-name" v-model="form.name" type="text" />
          </div>
        </div>

        <div class="col-md-4">
          <div class="field">
            <label for="cat-parent">Parent</label>
            <select id="cat-parent" v-model="form.parentId" class="form-select">
              <option :value="null">None (top level)</option>
              <option
                v-for="c in categories.filter((x) => x.id !== editingId)"
                :key="c.id"
                :value="c.id"
              >
                {{ c.name }}
              </option>
            </select>
          </div>
        </div>

        <div class="col-md-4">
          <div class="field">
            <label for="cat-order">Sort order</label>
            <input id="cat-order" v-model.number="form.sortOrder" type="number" min="0" />
          </div>
        </div>

        <div class="col-12">
          <div class="field">
            <label for="cat-description">Description</label>
            <input id="cat-description" v-model="form.description" type="text" />
          </div>
        </div>

        <div class="col-12">
          <label class="d-flex align-items-center gap-2">
            <input v-model="form.isActive" type="checkbox" />
            <span class="small">Visible in the storefront</span>
          </label>
        </div>
      </div>

      <div class="d-flex gap-2 mt-3">
        <button class="btn-pill btn-accent" type="submit" :disabled="saving">
          {{ saving ? 'Saving…' : editingId !== null ? 'Save changes' : 'Create category' }}
        </button>
        <button class="btn-pill btn-ghost" type="button" @click="reset">Cancel</button>
      </div>
    </form>

    <LoadingSpinner v-if="loading" label="Loading categories…" />

    <div v-else-if="error" class="error-state" role="alert">
      <h3>Something went wrong</h3>
      <p>{{ error }}</p>
    </div>

    <table v-else class="data-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Slug</th>
          <th>Parent</th>
          <th class="text-end">Products</th>
          <th>Visible</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr v-for="category in categories" :key="category.id">
          <td class="fw-semibold">{{ category.name }}</td>
          <td class="small text-secondary">{{ category.slug }}</td>
          <td>{{ category.parentName ?? '—' }}</td>
          <td class="text-end">{{ category.productCount }}</td>
          <td>{{ category.isActive ? 'Yes' : 'No' }}</td>
          <td class="text-end">
            <button class="btn-pill btn-ghost py-1 px-3" type="button" @click="startEdit(category)">
              Edit
            </button>
            <button class="btn-pill btn-ghost py-1 px-3" type="button" @click="remove(category)">
              Delete
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>