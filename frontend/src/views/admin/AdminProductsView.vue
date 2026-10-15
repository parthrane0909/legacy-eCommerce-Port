<script setup lang="ts">
/** Admin product management: list, create, edit and delete. */
import { onMounted, ref } from 'vue'
import * as adminApi from '@/api/admin'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'
import { formatCurrency } from '@/utils/format'
import AdminProductForm from '@/components/admin/AdminProductForm.vue'
import Pagination from '@/components/common/Pagination.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import type { AdminCategory, AdminProduct, AdminProductRequest } from '@/types'

const ui = useUiStore()

const products = ref<AdminProduct[]>([])
const categories = ref<AdminCategory[]>([])
const page = ref(1)
const totalPages = ref(0)
const totalCount = ref(0)
const search = ref('')

const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const editing = ref<AdminProduct | null>(null)
const showForm = ref(false)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const result = await adminApi.getAdminProducts(page.value, 20, search.value)
    products.value = result.items
    totalPages.value = result.totalPages
    totalCount.value = result.totalCount
  } catch (cause) {
    error.value = toErrorMessage(cause, 'Products could not be loaded.')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await load()
  categories.value = await adminApi.getAdminCategories().catch(() => [])
})

function startCreate(): void {
  editing.value = null
  showForm.value = true
}

function startEdit(product: AdminProduct): void {
  editing.value = product
  showForm.value = true
}

async function save(payload: AdminProductRequest): Promise<void> {
  saving.value = true
  try {
    if (editing.value) {
      await adminApi.updateProduct(editing.value.id, payload)
      ui.notifySuccess('Product updated.')
    } else {
      await adminApi.createProduct(payload)
      ui.notifySuccess('Product created.')
    }

    showForm.value = false
    editing.value = null
    await load()
  } catch (cause) {
    ui.notifyError(toErrorMessage(cause, 'The product could not be saved.'))
  } finally {
    saving.value = false
  }
}

async function remove(product: AdminProduct): Promise<void> {
  try {
    await adminApi.deleteProduct(product.id)
    ui.notifySuccess(
      product.isActive ? 'Product deleted.' : 'Product removed.',
    )
    await load()
  } catch (cause) {
    ui.notifyError(toErrorMessage(cause, 'The product could not be deleted.'))
  }
}

async function onSearch(): Promise<void> {
  page.value = 1
  await load()
}

async function goToPage(target: number): Promise<void> {
  page.value = target
  await load()
}
</script>

<template>
  <div>
    <div class="d-flex flex-wrap gap-2 justify-content-between align-items-center mb-3">
      <h2 class="h5 mb-0">Products ({{ totalCount }})</h2>
      <div class="d-flex gap-2">
        <form class="d-flex gap-2" role="search" @submit.prevent="onSearch">
          <label class="visually-hidden" for="admin-search">Search the product list</label>
          <input
            id="admin-search"
            data-testid="admin-product-search"
            v-model="search"
            class="form-control form-control-sm"
            style="min-width: 200px"
            type="search"
            placeholder="Search…"
          />
          <button class="btn-pill" type="submit">Filter</button>
        </form>
        <button class="btn-pill btn-accent" type="button" @click="startCreate">New product</button>
      </div>
    </div>

    <AdminProductForm
      v-if="showForm"
      :product="editing"
      :categories="categories"
      :saving="saving"
      @submit="save"
      @cancel="showForm = false"
    />

    <LoadingSpinner v-if="loading" label="Loading products…" />

    <div v-else-if="error" class="error-state" role="alert">
      <h3>Something went wrong</h3>
      <p>{{ error }}</p>
      <button class="btn-pill mt-2" type="button" @click="load">Try again</button>
    </div>

    <table v-else class="data-table">
      <thead>
        <tr>
          <th>Product</th>
          <th>Category</th>
          <th class="text-end">Price</th>
          <th class="text-end">Stock</th>
          <th>Flags</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr v-for="product in products" :key="product.id">
          <td>
            <img
              :src="product.imageUrl"
              :alt="product.name"
              style="width: 44px; height: 44px; object-fit: cover; border-radius: 8px"
            />
            <div class="fw-semibold">{{ product.name }}</div>
            <div class="small text-secondary">{{ product.slug }}</div>
          </td>
          <td>{{ product.categoryName }}</td>
          <td class="text-end">
            {{ formatCurrency(product.price) }}
            <div v-if="product.discountPercent" class="small text-secondary">
              -{{ product.discountPercent }}%
            </div>
          </td>
          <td class="text-end">{{ product.stock }}</td>
          <td>
            <span v-if="product.isFeatured" class="badge-feat me-1" style="position: static">Featured</span>
            <span v-if="!product.isActive" class="badge-sale me-1" style="position: static">Inactive</span>
          </td>
          <td class="text-end">
            <button class="btn-pill btn-ghost py-1 px-3" type="button" @click="startEdit(product)">
              Edit
            </button>
            <button class="btn-pill btn-ghost py-1 px-3" type="button" @click="remove(product)">
              Delete
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <Pagination :page="page" :total-pages="totalPages" :disabled="loading" @change="goToPage" />
  </div>
</template>