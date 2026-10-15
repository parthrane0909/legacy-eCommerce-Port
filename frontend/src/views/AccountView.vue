<script setup lang="ts">
/** Account dashboard: profile, address book and links to order history. */
import { onMounted, ref, reactive } from 'vue'
import * as accountApi from '@/api/account'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'
import AddressForm from '@/components/account/AddressForm.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import type { Address, AddressInput, Profile } from '@/types'

const auth = useAuthStore()
const ui = useUiStore()

const profile = ref<Profile | null>(null)
const addresses = ref<Address[]>([])
const loading = ref(true)
const savingProfile = ref(false)
const savingAddress = ref(false)
const editingAddressId = ref<number | null>(null)
const serverError = ref<string | null>(null)
const fieldErrors = ref<Record<string, string>>({})

const profileForm = reactive({ firstName: '', lastName: '', email: '' })
const addressForm = reactive<AddressInput>(emptyAddress())
const addressIsDefault = ref(false)

function emptyAddress(): AddressInput {
  return {
    firstName: '',
    lastName: '',
    line1: '',
    line2: '',
    city: '',
    state: '',
    postalCode: '',
    country: 'India',
    phone: '',
  }
}

onMounted(async () => {
  try {
    const [loadedProfile, loadedAddresses] = await Promise.all([
      accountApi.getProfile(),
      accountApi.getAddresses(),
    ])

    profile.value = loadedProfile
    Object.assign(profileForm, {
      firstName: loadedProfile.firstName,
      lastName: loadedProfile.lastName,
      email: loadedProfile.email,
    })
    addresses.value = loadedAddresses
  } catch (cause) {
    serverError.value = toErrorMessage(cause, 'Your account could not be loaded.')
  } finally {
    loading.value = false
  }
})

async function saveProfile(): Promise<void> {
  savingProfile.value = true
  serverError.value = null

  try {
    const updated = await accountApi.updateProfile({ ...profileForm })
    profile.value = updated
    Object.assign(profileForm, {
      firstName: updated.firstName,
      lastName: updated.lastName,
      email: updated.email,
    })
    ui.notifySuccess('Profile updated.')
  } catch (cause) {
    serverError.value = toErrorMessage(cause, 'Your profile could not be updated.')
  } finally {
    savingProfile.value = false
  }
}

function startAdd(): void {
  editingAddressId.value = 0
  Object.assign(addressForm, emptyAddress())
  addressIsDefault.value = false
  fieldErrors.value = {}
}

function startEdit(address: Address): void {
  editingAddressId.value = address.id
  Object.assign(addressForm, {
    firstName: address.firstName,
    lastName: address.lastName,
    line1: address.line1,
    line2: address.line2 ?? '',
    city: address.city,
    state: address.state,
    postalCode: address.postalCode,
    country: address.country,
    phone: address.phone ?? '',
  })
  addressIsDefault.value = address.isDefault
}

async function saveAddress(): Promise<void> {
  savingAddress.value = true
  fieldErrors.value = {}

  try {
    const payload = { ...addressForm, isDefault: addressIsDefault.value }

    if (editingAddressId.value === 0) {
      await accountApi.createAddress(payload)
      addresses.value = await accountApi.getAddresses()
      ui.notifySuccess('Address added.')
    } else if (editingAddressId.value !== null) {
      await accountApi.updateAddress(editingAddressId.value, payload)
      addresses.value = await accountApi.getAddresses()
      ui.notifySuccess('Address updated.')
    }

    editingAddressId.value = null
  } catch (cause) {
    const message = toErrorMessage(cause, 'The address could not be saved.')
    serverError.value = message
    ui.notifyError(message)
  } finally {
    savingAddress.value = false
  }
}

async function removeAddress(address: Address): Promise<void> {
  try {
    await accountApi.deleteAddress(address.id)
    addresses.value = addresses.value.filter((a) => a.id !== address.id)
    ui.notifySuccess('Address deleted.')
  } catch (cause) {
    ui.notifyError(toErrorMessage(cause, 'The address could not be deleted.'))
  }
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <p class="kicker">Account</p>
      <h1 class="section-title mb-4">My account</h1>

      <LoadingSpinner v-if="loading" label="Loading your account…" />

      <div v-else class="cart-layout">
        <div class="d-flex flex-column gap-4">
          <div v-if="serverError" class="alert alert-danger py-2" role="alert">{{ serverError }}</div>

          <!-- Profile -->
          <div class="summary-card" style="position: static">
            <h2 class="h6 mb-3">Profile</h2>

            <div class="row g-3">
              <div class="col-md-6">
                <div class="field">
                  <label for="profile-first">First name</label>
                  <input id="profile-first" v-model="profileForm.firstName" type="text" />
                </div>
              </div>
              <div class="col-md-6">
                <div class="field">
                  <label for="profile-last">Last name</label>
                  <input id="profile-last" v-model="profileForm.lastName" type="text" />
                </div>
              </div>
              <div class="col-12">
                <div class="field">
                  <label for="profile-email">Email</label>
                  <input id="profile-email" v-model="profileForm.email" type="email" />
                </div>
              </div>
            </div>

            <div class="d-flex align-items-center gap-3">
              <button
                class="btn-pill btn-accent"
                type="button"
                :disabled="savingProfile"
                @click="saveProfile"
              >
                {{ savingProfile ? 'Saving…' : 'Save changes' }}
              </button>
              <span class="small text-secondary">
                Signed in as {{ auth.user?.role }} · orders at
                <RouterLink :to="{ name: 'order-history' }">/account/orders</RouterLink>
              </span>
            </div>
          </div>

          <!-- Address book -->
          <div class="summary-card" style="position: static">
            <div class="d-flex justify-content-between align-items-center mb-3">
              <h2 class="h6 mb-0">Addresses</h2>
              <button class="btn-pill" type="button" @click="startAdd">Add address</button>
            </div>

            <p v-if="addresses.length === 0 && editingAddressId === null" class="text-secondary small">
              You have not saved any addresses yet.
            </p>

            <ul v-else class="list-unstyled mb-0 d-flex flex-column gap-2">
              <li
                v-for="address in addresses"
                :key="address.id"
                class="border rounded-3 p-3 d-flex justify-content-between align-items-start"
              >
                <div>
                  <strong>{{ address.fullName }}</strong>
                  <span v-if="address.isDefault" class="badge-feat ms-2" style="position: static">
                    Default
                  </span>
                  <div class="small text-secondary">
                    {{ address.line1 }}<br />
                    {{ address.city }}, {{ address.state }} {{ address.postalCode }}<br />
                    {{ address.country }}<span v-if="address.phone"> · {{ address.phone }}</span>
                  </div>
                </div>
                <div class="d-flex gap-2">
                  <button class="btn-pill btn-ghost py-1 px-3" type="button" @click="startEdit(address)">
                    Edit
                  </button>
                  <button
                    class="btn-pill btn-ghost py-1 px-3"
                    type="button"
                    @click="removeAddress(address)"
                  >
                    Delete
                  </button>
                </div>
              </li>
            </ul>

            <div v-if="editingAddressId !== null" class="mt-4">
              <hr />
              <h3 class="h6 mb-3">
                {{ editingAddressId === 0 ? 'New address' : 'Edit address' }}
              </h3>

              <AddressForm
                v-model="addressForm"
                heading="Address"
                show-default-toggle
                :is-default="addressIsDefault"
                :errors="fieldErrors"
                @update:is-default="addressIsDefault = $event"
              />

              <div class="d-flex gap-2 mt-3">
                <button
                  class="btn-pill btn-accent"
                  type="button"
                  :disabled="savingAddress"
                  @click="saveAddress"
                >
                  {{ savingAddress ? 'Saving…' : 'Save address' }}
                </button>
                <button
                  class="btn-pill btn-ghost"
                  type="button"
                  @click="editingAddressId = null"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>

        <aside class="summary-card">
          <h2 class="h6 mb-3">Quick links</h2>
          <div class="d-flex flex-column gap-2">
            <RouterLink class="btn-pill justify-content-center" :to="{ name: 'order-history' }">
              Order history
            </RouterLink>
            <RouterLink class="btn-pill justify-content-center" :to="{ name: 'cart' }">
              Shopping cart
            </RouterLink>
            <RouterLink class="btn-pill justify-content-center" :to="{ name: 'products' }">
              Continue shopping
            </RouterLink>
          </div>
        </aside>
      </div>
    </div>
  </section>
</template>