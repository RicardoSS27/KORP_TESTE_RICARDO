export interface Produto {
  id: number;
  codigo: string;
  descricao: string;
  saldo: number;
}

export interface ItemNota {
  id?: number;
  produtoId: number;
  quantidade: number;
}

export interface NotaFiscal {
  id: number;
  numero: number;
  status: string;
  itens: ItemNota[];
}

export interface ProdutoPayload {
  codigo: string;
  descricao: string;
  saldo: number;
}

export interface NotaPayload {
  numero: number;
  itens: ItemNota[];
}
