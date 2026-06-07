import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
    getProductsOptions,
    getProductsQueryKey,
    postProductsMutation,
    deleteProductsByIdMutation,
} from '@/client/@tanstack/react-query.gen';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';

export function ProductsPage() {
    const qc = useQueryClient();
    const { data: products, isPending, error } = useQuery({ ...getProductsOptions() });
    const [name, setName] = useState('');
    const [price, setPrice] = useState(0);

    const invalidate = () => void qc.invalidateQueries({ queryKey: getProductsQueryKey() });
    const create = useMutation({
        ...postProductsMutation(),
        onSuccess: () => {
            setName('');
            setPrice(0);
            invalidate();
        },
    });
    const remove = useMutation({ ...deleteProductsByIdMutation(), onSuccess: invalidate });

    return (
        <div className="space-y-4">
            <h1 className="text-xl font-semibold">Produits</h1>
            <form
                className="flex items-end gap-2"
                onSubmit={(e) => {
                    e.preventDefault();
                    if (!name || !Number.isFinite(price) || price <= 0 || create.isPending) return;
                    create.mutate({ body: { name, price } });
                }}
            >
                <Input placeholder="Nom" value={name} onChange={(e) => setName(e.target.value)} />
                <Input
                    type="number"
                    placeholder="Prix"
                    value={price}
                    onChange={(e) => setPrice(Number(e.target.value))}
                />
                <Button type="submit" disabled={create.isPending}>
                    Ajouter
                </Button>
            </form>
            {(create.isError || remove.isError) && (
                <p className="text-sm text-destructive">Opération échouée.</p>
            )}

            {isPending ? (
                <p>Chargement…</p>
            ) : error ? (
                <p>Échec du chargement.</p>
            ) : (
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Nom</TableHead>
                            <TableHead>Prix</TableHead>
                            <TableHead />
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {products?.map((p) => (
                            <TableRow key={p.id}>
                                <TableCell>{p.name}</TableCell>
                                <TableCell>{p.price}</TableCell>
                                <TableCell>
                                    <Button
                                        variant="ghost"
                                        disabled={remove.isPending}
                                        onClick={() => remove.mutate({ path: { id: p.id } })}
                                    >
                                        Supprimer
                                    </Button>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            )}
        </div>
    );
}
